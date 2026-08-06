using PolyPersist.Net.Core;
using Sales.OrderManagement;
using Sales.OrderManagement.Context.Implementations;
using Sales.OrderManagement.Order;
using ServiceKit.Net;

namespace SampleWebShop.Tests
{
    // The saga, without a Temporal server.
    //
    // What runs inside a workflow context - the generated FulfilOrderSteps - cannot be exercised
    // here: every step goes through Workflow.ExecuteActivityAsync, which needs a live worker. What
    // CAN be checked without one is everything the model decided: which activities exist, what
    // compensates what, in which order the rollback runs, and the retry and deadline the model
    // declared. That is the part a regression would silently change.
    [TestClass]
    public class FulfilOrderTests
    {
        private static readonly EntityId<OrderHeader> _order = "order-1";

        [TestMethod]
        public async Task A_failure_after_the_charge_rolls_both_steps_back_in_reverse()
        {
            // The order the workflow body establishes - reserve, charge, ship - with the sample's own
            // activities behind it. Shipping is what fails, and shipping is the step with no
            // compensation, so the two before it are what must come back.
            var log = new List<string>();
            var activities = (IFulfilOrderActivities)new FulfilOrderActivities(null);
            var saga = new WorkflowSaga();

            var reservationId = await activities.reserveStock(_order, "sku-1", 2);
            saga.Push(nameof(activities.reserveStock), async () =>
            {
                await activities.releaseStock(_order, "sku-1", reservationId);
                log.Add($"releaseStock:{reservationId}");
            });

            var chargeId = await activities.chargeCard(_order, 100);
            saga.Push(nameof(activities.chargeCard), async () =>
            {
                await activities.refundCard(_order, chargeId);
                log.Add($"refundCard:{chargeId}");
            });

            var shipping = new ApplicationException("the courier refused the parcel");
            await saga.CompensateAsync(shipping);

            CollectionAssert.AreEqual(new[] { $"refundCard:{chargeId}", $"releaseStock:{reservationId}" }, log);
            Assert.AreEqual(0, saga.PendingCount);
        }

        [TestMethod]
        public async Task A_cancellation_before_the_charge_releases_only_the_reservation()
        {
            // The cancel signal is honoured after the first step, so nothing has been charged yet
            // and there is nothing to refund.
            var log = new List<string>();
            var activities = (IFulfilOrderActivities)new FulfilOrderActivities(null);
            var saga = new WorkflowSaga();

            var reservationId = await activities.reserveStock(_order, "sku-1", 2);
            saga.Push(nameof(activities.reserveStock), async () =>
            {
                await activities.releaseStock(_order, "sku-1", reservationId);
                log.Add("releaseStock");
            });

            await saga.CompensateAsync(new ApplicationException("The order was cancelled: out of stock"));

            CollectionAssert.AreEqual(new[] { "releaseStock" }, log);
        }

        [TestMethod]
        public async Task The_compensation_is_handed_the_id_the_step_returned()
        {
            // Nothing passes the reservation id by hand: the model says releaseStock compensates
            // reserveStock, and the generated facade binds the argument from what the step returned.
            var seen = new List<string>();
            var activities = (IFulfilOrderActivities)new FulfilOrderActivities(null);
            var saga = new WorkflowSaga();

            var reservationId = await activities.reserveStock(_order, "sku-1", 1);
            saga.Push(nameof(activities.reserveStock), () => { seen.Add(reservationId); return Task.CompletedTask; });

            await saga.CompensateAsync();

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(reservationId, seen[0]);
            Assert.IsTrue(Guid.TryParse(seen[0], out _));
        }

        [TestMethod]
        public async Task Shipping_answers_with_a_tracking_number()
        {
            var activities = (IFulfilOrderActivities)new FulfilOrderActivities(null);

            var trackingNumber = await activities.shipOrder(_order, "6720 Szeged, Kárász utca 1.");

            StringAssert.StartsWith(trackingNumber, "TRK-");
        }

        [TestMethod]
        public void A_card_charge_is_attempted_once_and_given_a_short_deadline()
        {
            // Both are business properties, so both are modelled: @retry( 1 ) and @timeout( "30s" ).
            var options = FulfilOrderDefaults.For("chargeCard");

            Assert.AreEqual(1, options.RetryPolicy.MaximumAttempts);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.ScheduleToCloseTimeout);
        }

        [TestMethod]
        public void Every_other_step_keeps_the_workflow_default()
        {
            foreach (var step in new[] { "reserveStock", "releaseStock", "refundCard", "shipOrder" })
            {
                var options = FulfilOrderDefaults.For(step);

                Assert.AreEqual(3, options.RetryPolicy.MaximumAttempts, step);
                Assert.AreEqual(TimeSpan.FromSeconds(600), options.ScheduleToCloseTimeout, step);
            }
        }

        [TestMethod]
        public void The_workflow_registers_itself_with_its_activities_on_its_own_queue()
        {
            var registry = FulfilOrderRegistration.Register(new WorkflowRegistry());

            var queue = registry.FindQueue(FulfilOrderRegistration.TaskQueue);

            Assert.IsNotNull(queue);
            Assert.AreEqual("OrderManagement.FulfilOrder", FulfilOrderRegistration.TaskQueue);
            CollectionAssert.Contains(queue.WorkflowTypes.ToList(), typeof(FulfilOrderWorkflow));
            CollectionAssert.Contains(queue.ActivityServiceTypes.ToList(), typeof(IFulfilOrderActivities));
        }
    }
}

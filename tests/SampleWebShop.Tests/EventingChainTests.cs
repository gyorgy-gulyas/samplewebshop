using Microsoft.Extensions.DependencyInjection;
using Sales.OrderManagement;
using Sales.Tracking;
using Sales.Tracking.Context.Implementations;
using Sales.Tracking.OrderTrackingEntry;
using ServiceKit.Net;
using ServiceKit.Net.Eventing;

namespace SampleWebShop.Tests
{
    /// <summary>
    /// The whole chain, in the real host: place an order and a different context reacts to it.
    ///
    /// Nothing here is stubbed and nothing is called directly. Between the placeOrder below and the
    /// tracking entry it waits for, every step is code nobody wrote by hand - the root's Record
    /// overload, the outbox append inside the commit, the relay, the broker, the dispatcher, the
    /// generated publisher that runs the translation, and the generated handler that delivers to the
    /// reaction. Until a test ran the whole thing end to end, none of it had ever been proven to
    /// connect: each piece has its own unit tests, and every one of them would still pass with the
    /// chain broken in the middle.
    /// </summary>
    [TestClass]
    public class EventingChainTests
    {
        private static IOrderIF_v1.OrderDTO AnOrder()
        {
            return new IOrderIF_v1.OrderDTO()
            {
                orderingDate = "2026-08-09",
                orderStatus = IOrderIF_v1.OrderStatuses.Draft,
                totalPrice = 250,
                customerData = new IOrderIF_v1.OrderDTO.CustomerDataDTO()
                {
                    customerId = "customer-chain",
                    customerName = "Chain",
                },
                items = new List<IOrderIF_v1.OrderItemDTO>()
                {
                    new()
                    {
                        productId = "p1",
                        productName = "Widget",
                        quantity = 5,
                        unitPrice = 50,
                        subTotalPrice = 250,
                        deliveryStatus = IOrderIF_v1.OrderItemDTO.DeliveryStatuses.NotDelivered,
                    },
                },
            };
        }

        private static async Task<IOrderIF_v1.OrderDTO> Place()
        {
            // Through a scope, because the surfaces are scoped - the recorder a save drains belongs
            // to one unit of work, and this is what a request would do.
            using var scope = ServiceHostFixture.Services.CreateScope();
            var v1 = scope.ServiceProvider.GetRequiredService<IOrderIF_v1>();

            var placed = await v1.placeOrder(new CallingContext(), AnOrder());
            Assert.IsTrue(placed.IsSuccess());
            return placed.Value;
        }

        // Delivery is asynchronous by design - the relay polls, and the point of the outbox is that
        // the fact leaves AFTER the commit rather than during it. So the test waits for an outcome
        // instead of assuming one, and fails on a timeout rather than on a guessed sleep.
        private static async Task<TrackingEntry> WaitForTracking(string orderId, TimeSpan timeout)
        {
            var entries = ServiceHostFixture.Services.GetRequiredService<TrackingStoreContext>().Entries;
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                var found = entries.Query(orderId).FirstOrDefault();
                if (found != null)
                    return found;

                await Task.Delay(100);
            }

            return null;
        }

        [TestMethod]
        public async Task An_order_placed_in_one_context_opens_tracking_in_another()
        {
            var placed = await Place();

            var tracked = await WaitForTracking(placed.id, TimeSpan.FromSeconds(30));

            Assert.IsNotNull(tracked, $"No tracking entry appeared for order '{placed.id}'. The chain from the aggregate to the other context is broken.");
            Assert.AreEqual(placed.id, (string)tracked.order);
            Assert.AreEqual(TrackingStatuses.Pending, tracked.trackingStatus);
        }

        [TestMethod]
        public async Task What_crosses_the_boundary_is_the_published_contract_and_not_the_internal_fact()
        {
            var placed = await Place();
            await WaitForTracking(placed.id, TimeSpan.FromSeconds(30));

            // The reaction is bound to the PUBLISHED type. That is the assertion: had the handler
            // been bound to Order.OrderPlaced, Tracking would be reading OrderManagement's private
            // language and every rename inside that context would be a wire incident here.
            var registry = ServiceHostFixture.Services.GetRequiredService<EventSubscriptionRegistry>();

            var tracking = registry.Subscriptions
                .Where(subscription => subscription.HandlerType == typeof(OnOrderPlacedHandler))
                .ToArray();

            Assert.AreEqual(1, tracking.Length);
            Assert.AreEqual("Sales.OrderManagement.OrderIF.v1.OrderPlaced.v1", tracking[0].SchemaId);
            Assert.AreEqual(typeof(IOrderIF_v1.OrderPlaced_v1), tracking[0].EventType);
        }

        [TestMethod]
        public void Both_published_versions_are_translated_and_registered()
        {
            // One internal fact, two published contracts. The publishers are generated and register
            // themselves; if either translation had been declared and never written, this assembly
            // would not have compiled.
            var registry = ServiceHostFixture.Services.GetRequiredService<EventSubscriptionRegistry>();

            var publishers = registry.Subscriptions
                .Where(subscription => subscription.EventType == typeof(Sales.OrderManagement.Order.OrderPlaced))
                .Select(subscription => subscription.HandlerType.Name)
                .OrderBy(name => name)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "OrderIF_v1OrderPlaced_v1Publisher", "OrderIF_v2OrderPlaced_v2Publisher" },
                publishers);
        }

        [TestMethod]
        public async Task The_translation_says_what_the_outside_world_gets()
        {
            var placed = await Place();
            await WaitForTracking(placed.id, TimeSpan.FromSeconds(30));

            // v1 publishes the amount under the name the outside world uses, and does NOT publish
            // who ordered. This is the hand-written translation being checked, not a field copy.
            var translated = OrderIF_v1Translations.ToOrderPlaced_v1(new Sales.OrderManagement.Order.OrderPlaced()
            {
                orderId = placed.id,
                customerId = "customer-chain",
                totalPrice = 250,
            });

            Assert.AreEqual(placed.id, translated.orderId);
            Assert.AreEqual(250m, translated.totalAmount);

            // v2 is a different promise to a different audience, from the same fact.
            var v2 = OrderIF_v2Translations.ToOrderPlaced_v2(new Sales.OrderManagement.Order.OrderPlaced()
            {
                orderId = placed.id,
                customerId = "customer-chain",
                totalPrice = 250,
            });

            Assert.AreEqual("customer-chain", v2.customerId);
        }
    }
}

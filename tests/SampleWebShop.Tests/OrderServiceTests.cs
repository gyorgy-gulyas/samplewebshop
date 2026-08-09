using PolyPersist.Net.Common;
using Sales.OrderManagement;
using Sales.OrderManagement.Context.Implementations;
using Sales.OrderManagement.Order;
using ServiceKit.Net;
using ServiceKit.Net.Eventing;
using ServiceKit.Net.Eventing.InMemory;

namespace SampleWebShop.Tests
{
    // The application service with a real store behind it. Nothing is mocked: the store is the
    // in-memory one, so what these tests observe is what the service does in production - including
    // the validation, which the store performs and nobody else.
    [TestClass]
    public class OrderServiceTests
    {
        private IOrderService _service;
        private InMemoryOutboxStore _outbox;

        [TestInitialize]
        public void Setup()
        {
            // A real outbox and a real recorder, not stubs: placing an order now records a fact, and
            // a test that took a null recorder would be testing a service nobody runs.
            _outbox = new InMemoryOutboxStore();
            _service = new OrderService(
                new OrderStoreContext(new TestStoreProvider()),
                _outbox,
                new EventRecorder(new JsonEventSerializer()),
                null);
        }

        private static OrderHeader AnOrder(params decimal[] quantities)
        {
            return new OrderHeader()
            {
                customer = "customer-1",
                orderingDate = new DateOnly(2026, 8, 6),
                status = OrderStatuses.Draft,
                totalPrice = 100,
                humanKey = "WS-1",
                partnerData = string.Empty,
                items = quantities.Select((quantity, index) => new OrderItem()
                {
                    productId = $"p{index}",
                    productName = $"product {index}",
                    quantity = quantity,
                    unitPrice = 10,
                    subTotalPrice = 10 * quantity,
                }).ToList(),
            };
        }

        [TestMethod]
        public async Task An_order_without_an_id_gets_one_and_is_released()
        {
            var placed = await _service.placeOrder(new CallingContext(), AnOrder(1));

            Assert.IsTrue(placed.IsSuccess());
            Assert.IsFalse(string.IsNullOrEmpty(placed.Value.id));
            Assert.AreEqual(OrderStatuses.Released, placed.Value.status);
        }

        [TestMethod]
        public async Task A_placed_order_can_be_read_back()
        {
            var placed = await _service.placeOrder(new CallingContext(), AnOrder(2));

            var found = await _service.getOrder(new CallingContext(), placed.Value.id);

            Assert.IsTrue(found.IsSuccess());
            Assert.AreEqual(placed.Value.id, found.Value.id);
            Assert.AreEqual(1, found.Value.items.Count);
        }

        [TestMethod]
        public async Task An_order_that_does_not_exist_answers_not_found()
        {
            var found = await _service.getOrder(new CallingContext(), "no-such-order");

            Assert.IsTrue(found.IsFailed());
            Assert.AreEqual(Statuses.NotFound, found.Status);
        }

        [TestMethod]
        public async Task A_broken_item_is_refused_by_the_store_and_names_its_path()
        {
            // The service writes no validation of its own (SWS/V11): the store validates before it
            // writes. This is the test that would have caught the zero-quantity order the sample
            // once accepted with a 200.
            var order = AnOrder(1, 0, 3);

            var failure = await Assert.ThrowsExceptionAsync<ValidationExeption>(
                () => _service.placeOrder(new CallingContext(), order));

            Assert.AreEqual(1, failure.ValidationErrors.Count);
            Assert.AreEqual("items[1].quantity", failure.ValidationErrors[0].Path);
        }

        [TestMethod]
        public async Task Every_broken_field_comes_back_at_once()
        {
            // A form with three bad fields is the ordinary case, and it deserves one round trip.
            var order = AnOrder(0, 0);
            order.totalPrice = -1;

            var failure = await Assert.ThrowsExceptionAsync<ValidationExeption>(
                () => _service.placeOrder(new CallingContext(), order));

            CollectionAssert.AreEquivalent(
                new[] { "totalPrice", "items[0].quantity", "items[1].quantity" },
                failure.ValidationErrors.Select(error => error.Path).ToArray());
        }

        [TestMethod]
        public async Task Placing_an_order_queues_the_internal_fact()
        {
            var placed = await _service.placeOrder(new CallingContext(), AnOrder(1));

            var recorded = _outbox.All;
            Assert.AreEqual(1, recorded.Count);
            Assert.AreEqual("Sales.OrderManagement.Order.OrderPlaced", recorded[0].SchemaId);
            // The ordering scope is the order's own identity, so one order's facts keep their order
            // and nothing is promised between two different orders.
            Assert.AreEqual(placed.Value.id, recorded[0].PartitionKey);
        }

        [TestMethod]
        public async Task A_refused_order_tells_nobody_anything()
        {
            // The failure mode the outbox exists to prevent, from the other side: the state was not
            // saved, so the fact must not be waiting to go out either.
            await Assert.ThrowsExceptionAsync<ValidationExeption>(
                () => _service.placeOrder(new CallingContext(), AnOrder(0)));

            Assert.AreEqual(0, _outbox.All.Count);
        }

        [TestMethod]
        public async Task An_order_cannot_be_placed_twice()
        {
            var order = AnOrder(1);
            await _service.placeOrder(new CallingContext(), order);

            // The invariant is the root's, not the service's: there is no path to Released that
            // skips it.
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => _service.placeOrder(new CallingContext(), order));
        }

        [TestMethod]
        public async Task A_valid_order_is_not_refused()
        {
            var placed = await _service.placeOrder(new CallingContext(), AnOrder(1, 2, 3));

            Assert.IsTrue(placed.IsSuccess());
            Assert.AreEqual(3, placed.Value.items.Count);
        }
    }
}

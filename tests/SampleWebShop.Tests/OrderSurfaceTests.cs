using Sales.OrderManagement;
using Sales.OrderManagement.Context.Implementations;
using Sales.OrderManagement.Order;
using ServiceKit.Net;
using ServiceKit.Net.Eventing;
using ServiceKit.Net.Eventing.InMemory;

namespace SampleWebShop.Tests
{
    // Two published versions answering from ONE service. These tests are what makes that claim
    // checkable: v2 must add its field without v1 noticing, and neither may let a domain type reach
    // the wire.
    [TestClass]
    public class OrderSurfaceTests
    {
        private IOrderService _service;
        private IOrderIF_v1 _v1;
        private IOrderIF_v2 _v2;

        [TestInitialize]
        public void Setup()
        {
            _service = new OrderService(
                new OrderStoreContext(new TestStoreProvider()),
                new InMemoryOutboxStore(),
                new EventRecorder(new JsonEventSerializer()),
                null);
            _v1 = new OrderIF_v1(_service);
            _v2 = new OrderIF_v2(_service);
        }

        private static IOrderIF_v1.OrderDTO AV1Order()
        {
            return new IOrderIF_v1.OrderDTO()
            {
                orderingDate = "2026-08-06",
                orderStatus = IOrderIF_v1.OrderStatuses.Draft,
                totalPrice = 20,
                customerData = new IOrderIF_v1.OrderDTO.CustomerDataDTO() { customerId = "customer-1", customerName = "Kis Béla" },
                items = new()
                {
                    new IOrderIF_v1.OrderItemDTO() { productId = "p0", productName = "product", quantity = 2, unitPrice = 10, subTotalPrice = 20 },
                },
            };
        }

        [TestMethod]
        public async Task The_v1_surface_maps_both_ways()
        {
            var placed = await _v1.placeOrder(new CallingContext(), AV1Order());

            Assert.IsTrue(placed.IsSuccess());
            Assert.AreEqual("2026-08-06", placed.Value.orderingDate);
            Assert.AreEqual(IOrderIF_v1.OrderStatuses.Released, placed.Value.orderStatus);
            Assert.AreEqual("customer-1", placed.Value.customerData.customerId);
            Assert.AreEqual(1, placed.Value.items.Count);
            Assert.AreEqual(20m, placed.Value.items[0].subTotalPrice);
        }

        [TestMethod]
        public async Task An_order_placed_on_v1_can_be_read_on_v2()
        {
            // One model, two surfaces: the version is a property of the published interface, not of
            // what is stored.
            var placed = await _v1.placeOrder(new CallingContext(), AV1Order());

            var read = await _v2.getOrder(new CallingContext(), placed.Value.id);

            Assert.IsTrue(read.IsSuccess());
            Assert.AreEqual(placed.Value.id, read.Value.id);
            Assert.AreEqual(placed.Value.totalPrice, read.Value.totalPrice);
        }

        [TestMethod]
        public async Task The_v2_addition_is_carried_and_stays_out_of_v1()
        {
            var order = new IOrderIF_v2.OrderDTO()
            {
                orderingDate = "2026-08-06",
                orderStatus = IOrderIF_v2.OrderStatuses.Draft,
                totalPrice = 20,
                shippingCity = "Szeged",
                customerData = new IOrderIF_v2.OrderDTO.CustomerDataDTO() { customerId = "customer-1", customerName = "Kis Béla" },
                items = new()
                {
                    new IOrderIF_v2.OrderItemDTO() { productId = "p0", productName = "product", quantity = 2, unitPrice = 10, subTotalPrice = 20 },
                },
            };

            var placed = await _v2.placeOrder(new CallingContext(), order);
            Assert.IsTrue(placed.IsSuccess());

            var stored = await _service.getOrder(new CallingContext(), placed.Value.id);
            Assert.AreEqual("Szeged", stored.Value.partnerData);

            // v1 has no shipping city and must not grow one
            Assert.IsNull(typeof(IOrderIF_v1.OrderDTO).GetProperty("shippingCity"));
        }

        [TestMethod]
        public async Task A_failure_survives_the_surface_with_its_status()
        {
            // The surface only maps; it may not turn a 404 into anything else on the way out.
            var found = await _v1.getOrder(new CallingContext(), "no-such-order");

            Assert.IsTrue(found.IsFailed());
            Assert.AreEqual(Statuses.NotFound, found.Status);
            Assert.IsTrue(found.Errors.Count > 0);
        }

        [TestMethod]
        public async Task Setting_a_price_recomputes_the_line_total()
        {
            var item = new IOrderIF_v1.OrderItemDTO() { productId = "p0", productName = "product", quantity = 3, unitPrice = 10, subTotalPrice = 30 };

            var priced = await _v1.setPrice(new CallingContext(), item, 12);

            Assert.IsTrue(priced.IsSuccess());
            Assert.AreEqual(12m, priced.Value.unitPrice);
            Assert.AreEqual(36m, priced.Value.subTotalPrice);
        }
    }
}

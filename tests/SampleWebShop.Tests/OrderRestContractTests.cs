using Sales.OrderManagement;
using ServiceKit.Net;

namespace SampleWebShop.Tests
{
    // The contract test: the real host, started in process on a free port, called through the
    // GENERATED client. Everything in between - routing, the JSON body, the status mapping, the
    // error list - is code nobody wrote by hand, and until something sent a request over it none of
    // it had ever run.
    //
    [TestClass]
    public class OrderRestContractTests
    {
        private IOrderIF_v1 _client;

        [TestInitialize]
        public void Setup()
        {
            _client = new OrderIF_v1_RestClient(ServiceHostFixture.RestAddress);
        }

        private static IOrderIF_v1.OrderDTO AnOrder(params decimal[] quantities)
        {
            return new IOrderIF_v1.OrderDTO()
            {
                orderingDate = "2026-08-06",
                orderStatus = IOrderIF_v1.OrderStatuses.Draft,
                totalPrice = 100,
                customerData = new IOrderIF_v1.OrderDTO.CustomerDataDTO() { customerId = "customer-1", customerName = "Kis Béla" },
                items = quantities.Select((quantity, index) => new IOrderIF_v1.OrderItemDTO()
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
        public async Task A_good_order_is_accepted_over_the_wire()
        {
            var placed = await _client.placeOrder(new CallingContext(), AnOrder(1, 2));

            Assert.IsTrue(placed.IsSuccess(), string.Join(" | ", placed.Errors.Select(e => e.MessageText)));
            Assert.IsFalse(string.IsNullOrEmpty(placed.Value.id));
            Assert.AreEqual(IOrderIF_v1.OrderStatuses.Released, placed.Value.orderStatus);
        }

        [TestMethod]
        public async Task A_placed_order_can_be_fetched_by_its_id()
        {
            var placed = await _client.placeOrder(new CallingContext(), AnOrder(1));

            var found = await _client.getOrder(new CallingContext(), placed.Value.id);

            Assert.IsTrue(found.IsSuccess());
            Assert.AreEqual(placed.Value.id, found.Value.id);
        }

        [TestMethod]
        public async Task A_broken_item_comes_back_as_a_bad_request_naming_its_path()
        {
            // This is the whole chain: the store refuses, the generated controller turns the
            // exception into a 400 with a path per broken field, and the generated client reads the
            // list back instead of the first error only.
            var failed = await _client.placeOrder(new CallingContext(), AnOrder(1, 0, 3));

            Assert.IsTrue(failed.IsFailed());
            Assert.AreEqual(Statuses.BadRequest, failed.Status);
            Assert.AreEqual(1, failed.Errors.Count);
            Assert.AreEqual("items[1].quantity", failed.Errors[0].Path);
        }

        [TestMethod]
        public async Task Several_broken_fields_all_come_back_at_once()
        {
            var failed = await _client.placeOrder(new CallingContext(), AnOrder(0, 0));

            Assert.AreEqual(Statuses.BadRequest, failed.Status);
            CollectionAssert.AreEquivalent(
                new[] { "items[0].quantity", "items[1].quantity" },
                failed.Errors.Select(error => error.Path).ToArray());
        }

        [TestMethod]
        public async Task An_unknown_order_is_a_not_found_and_not_a_server_error()
        {
            var found = await _client.getOrder(new CallingContext(), "no-such-order");

            Assert.IsTrue(found.IsFailed());
            Assert.AreEqual(Statuses.NotFound, found.Status);
        }

        [TestMethod]
        public async Task An_id_that_needs_escaping_still_reaches_the_route()
        {
            // A slash in a route value used to be sent raw and matched a different route - or, once
            // the whole url was escaped, no route at all.
            var found = await _client.getOrder(new CallingContext(), "no/such order");

            Assert.AreEqual(Statuses.NotFound, found.Status);
        }

        [TestMethod]
        public async Task A_query_parameter_survives_the_trip()
        {
            var item = new IOrderIF_v1.OrderItemDTO() { productId = "p0", productName = "product", quantity = 3, unitPrice = 10, subTotalPrice = 30 };

            var priced = await _client.setPrice(new CallingContext(), item, 12.5m);

            Assert.IsTrue(priced.IsSuccess(), string.Join(" | ", priced.Errors.Select(e => e.MessageText)));
            Assert.AreEqual(12.5m, priced.Value.unitPrice);
            Assert.AreEqual(37.5m, priced.Value.subTotalPrice);
        }

        [TestMethod]
        public async Task An_operation_without_a_return_value_answers_success()
        {
            var answered = await _client.justOrder(new CallingContext(), "order-1");

            Assert.IsTrue(answered.IsSuccess());
        }
    }
}

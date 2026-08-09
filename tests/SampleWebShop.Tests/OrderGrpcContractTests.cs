using Sales.OrderManagement;
using ServiceKit.Net;

namespace SampleWebShop.Tests
{
    // The same interface, over the other transport, against the same running host.
    //
    // This is the test that proves the gRPC surface is SERVED and not merely compiled: the
    // generated controllers are mapped by attribute, and a controller that does not carry it makes
    // a host which starts happily and answers nothing.
    [TestClass]
    public class OrderGrpcContractTests
    {
        private IOrderIF_v1 _client;

        [TestInitialize]
        public void Setup()
        {
            _client = new OrderIF_v1_GrpcClient(ServiceHostFixture.Clients);
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
        public async Task An_order_placed_over_grpc_comes_back_with_its_id()
        {
            var placed = await _client.placeOrder(new CallingContext(), AnOrder(1, 2));

            Assert.IsTrue(placed.IsSuccess(), string.Join(" | ", placed.Errors.Select(e => e.MessageText)));
            Assert.IsFalse(string.IsNullOrEmpty(placed.Value.id));
            Assert.AreEqual(IOrderIF_v1.OrderStatuses.Released, placed.Value.orderStatus);
            Assert.AreEqual(2, placed.Value.items.Count);
        }

        [TestMethod]
        public async Task An_order_placed_over_grpc_can_be_read_over_rest()
        {
            // One model behind two transports: what goes in through gRPC comes out through REST.
            var placed = await _client.placeOrder(new CallingContext(), AnOrder(1));

            IOrderIF_v1 rest = new OrderIF_v1_RestClient(ServiceHostFixture.Clients);
            var overRest = await rest.getOrder(new CallingContext(), placed.Value.id);

            Assert.IsTrue(overRest.IsSuccess());
            Assert.AreEqual(placed.Value.id, overRest.Value.id);
        }

        [TestMethod]
        public async Task A_broken_item_names_the_same_path_over_grpc()
        {
            // The paths must not depend on the transport - a client on either one binds the same
            // field to the same error.
            var failed = await _client.placeOrder(new CallingContext(), AnOrder(1, 0, 3));

            Assert.IsTrue(failed.IsFailed());
            Assert.AreEqual(Statuses.BadRequest, failed.Status);
            Assert.AreEqual(1, failed.Errors.Count);
            Assert.AreEqual("items[1].quantity", failed.Errors[0].Path);
        }

        [TestMethod]
        public async Task An_unknown_order_is_a_not_found_over_grpc_too()
        {
            var found = await _client.getOrder(new CallingContext(), "no-such-order");

            Assert.IsTrue(found.IsFailed());
            Assert.AreEqual(Statuses.NotFound, found.Status, Explain(found));
        }

        [TestMethod]
        public async Task An_operation_without_a_return_value_answers_success_over_grpc()
        {
            var answered = await _client.justOrder(new CallingContext(), "order-1");

            Assert.IsTrue(answered.IsSuccess(), Explain(answered));
        }

        private static string Explain(Response response)
        {
            return string.Join(" | ", response.Errors.Select(error => $"{error.MessageText} :: {error.AdditionalInformation}"));
        }
    }
}

using PolyPersist;
using Sales.OrderManagement;
using Sales.OrderManagement.Order;

namespace SampleWebShop.Tests
{
    // The client and the server must name the same field the same way, or the form cannot bind the
    // error it is shown. That agreement is not obvious - the DTO validator and the domain validator
    // are generated from two different declarations - so it is asserted here rather than assumed.
    [TestClass]
    public class ValidationPathTests
    {
        private static IList<IValidationError> ErrorsOf(IValidable validable)
        {
            IList<IValidationError> errors = new List<IValidationError>();
            validable.Validate(errors);
            return errors;
        }

        [TestMethod]
        public void The_dto_validator_and_the_domain_validator_agree_on_the_path()
        {
            var dto = new IOrderIF_v1.OrderDTO()
            {
                items = new()
                {
                    new IOrderIF_v1.OrderItemDTO() { quantity = 1, unitPrice = 1 },
                    new IOrderIF_v1.OrderItemDTO() { quantity = 0, unitPrice = 1 },
                },
            };

            var order = new OrderHeader()
            {
                items = new()
                {
                    new OrderItem() { quantity = 1, unitPrice = 1, subTotalPrice = 1 },
                    new OrderItem() { quantity = 0, unitPrice = 1, subTotalPrice = 0 },
                },
            };

            var fromDto = ErrorsOf(dto).Select(error => error.Path).ToArray();
            var fromDomain = ErrorsOf(order).Select(error => error.Path).ToArray();

            CollectionAssert.AreEqual(new[] { "items[1].quantity" }, fromDto);
            CollectionAssert.AreEqual(new[] { "items[1].quantity" }, fromDomain);
        }

        [TestMethod]
        public void The_walk_numbers_every_element_it_passes()
        {
            var order = new OrderHeader()
            {
                items = new()
                {
                    new OrderItem() { quantity = 0, unitPrice = -1, subTotalPrice = 0 },
                    new OrderItem() { quantity = 5, unitPrice = 1, subTotalPrice = 5 },
                    new OrderItem() { quantity = -2, unitPrice = 1, subTotalPrice = -1 },
                },
            };

            var paths = ErrorsOf(order).Select(error => error.Path).ToArray();

            CollectionAssert.AreEquivalent(
                new[] { "items[0].quantity", "items[0].unitPrice", "items[2].quantity", "items[2].subTotalPrice" },
                paths);
        }

        [TestMethod]
        public void A_clean_order_reports_nothing()
        {
            var order = new OrderHeader()
            {
                totalPrice = 10,
                items = new() { new OrderItem() { quantity = 1, unitPrice = 10, subTotalPrice = 10 } },
            };

            IList<IValidationError> errors = new List<IValidationError>();

            Assert.IsTrue(order.Validate(errors));
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void An_error_names_the_type_and_the_member_beside_the_path()
        {
            // The path is what a form binds to; the type and member are what a log is read by. Both
            // are needed, which is why the path was added beside them and not instead of them.
            var order = new OrderHeader()
            {
                items = new() { new OrderItem() { quantity = 0, unitPrice = 1, subTotalPrice = 0 } },
            };

            var error = ErrorsOf(order).Single();

            Assert.AreEqual("items[0].quantity", error.Path);
            Assert.AreEqual("OrderItem", error.TypeOfEntity);
            Assert.AreEqual("quantity", error.MemberOfEntity);
        }
    }
}

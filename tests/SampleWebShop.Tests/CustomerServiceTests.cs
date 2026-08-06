using CustomerManagement.Customers;
using CustomerManagement.Customers.Context.Implementations;
using CustomerManagement.Customers.Customer;
using PolyPersist.Net.Common;
using ServiceKit.Net;

namespace SampleWebShop.Tests
{
    // The second context, with the rules that are about text rather than numbers: a length and a
    // pattern. They are declared in the .d3 and nowhere else - the service does not repeat them, and
    // these tests are what says so.
    [TestClass]
    public class CustomerServiceTests
    {
        private ICustomerService _service;

        [TestInitialize]
        public void Setup()
        {
            _service = new CustomerService(new CustomerStoreContext(new TestStoreProvider()));
        }

        [TestMethod]
        public async Task A_new_customer_starts_out_pending()
        {
            var registered = await _service.registerCustomer(new CallingContext(), "Kis Béla", "kis.bela@example.com");

            Assert.IsTrue(registered.IsSuccess());
            Assert.AreEqual(CustomerStatuses.Pending, registered.Value.status);
            Assert.IsFalse(string.IsNullOrEmpty(registered.Value.id));
        }

        [TestMethod]
        public async Task A_registered_customer_can_be_read_back()
        {
            var registered = await _service.registerCustomer(new CallingContext(), "Kis Béla", "kis.bela@example.com");

            var found = await _service.getCustomer(new CallingContext(), registered.Value.id);

            Assert.IsTrue(found.IsSuccess());
            Assert.AreEqual("kis.bela@example.com", found.Value.email);
        }

        [TestMethod]
        public async Task A_customer_that_does_not_exist_answers_not_found()
        {
            var found = await _service.getCustomer(new CallingContext(), "no-such-customer");

            Assert.IsTrue(found.IsFailed());
            Assert.AreEqual(Statuses.NotFound, found.Status);
        }

        [TestMethod]
        public async Task An_address_that_is_not_an_email_is_refused_by_its_pattern()
        {
            var failure = await Assert.ThrowsExceptionAsync<ValidationExeption>(
                () => _service.registerCustomer(new CallingContext(), "Kis Béla", "kis.bela.example.com"));

            Assert.AreEqual("email", failure.ValidationErrors.Single().Path);
        }

        [TestMethod]
        public async Task A_one_letter_name_is_refused_by_its_length()
        {
            var failure = await Assert.ThrowsExceptionAsync<ValidationExeption>(
                () => _service.registerCustomer(new CallingContext(), "K", "kis.bela@example.com"));

            Assert.AreEqual("name", failure.ValidationErrors.Single().Path);
        }

        [TestMethod]
        public async Task Both_bad_fields_are_reported_together()
        {
            var failure = await Assert.ThrowsExceptionAsync<ValidationExeption>(
                () => _service.registerCustomer(new CallingContext(), "K", "not-an-address"));

            CollectionAssert.AreEquivalent(
                new[] { "name", "email" },
                failure.ValidationErrors.Select(error => error.Path).ToArray());
        }
    }
}

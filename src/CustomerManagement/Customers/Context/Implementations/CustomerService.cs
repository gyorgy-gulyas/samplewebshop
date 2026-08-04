using CustomerManagement.Customers.Customer;
using PolyPersist.Net.Extensions;
using ServiceKit.Net;

namespace CustomerManagement.Customers.Context.Implementations
{
    public class CustomerService : ICustomerService
    {
        private readonly CustomerStoreContext _context;

        public CustomerService(CustomerStoreContext context)
        {
            _context = context;
        }

        async Task<Response<CustomerAccount>> ICustomerService.getCustomer(CallingContext ctx, string customerId)
        {
            var customer = await _context.Customers.Find(customerId, customerId).ConfigureAwait(false);
            if (customer == null)
                return new(new Error() { Status = Statuses.NotFound, MessageText = $"Customer {customerId} does not exist" });

            return new(customer);
        }

        async Task<Response<CustomerAccount>> ICustomerService.registerCustomer(CallingContext ctx, string name, string email)
        {
            var customer = new CustomerAccount()
            {
                id = Guid.NewGuid().ToString(),
                name = name,
                email = email,
                status = CustomerStatuses.Pending,
                billingAddress = new PostalAddress(),
            };

            // The e-mail pattern and the name length are model rules, declared in the .d3 as validate
            // expressions - this method only decides what to do when they are broken.
            var errors = new List<PolyPersist.IValidationError>();
            if (customer.Validate(errors) == false)
            {
                return new(new Error()
                {
                    Status = Statuses.BadRequest,
                    MessageText = "The customer is not valid",
                    AdditionalInformation = string.Join("; ", errors.Select(error => error.ErrorText)),
                });
            }

            await _context.Customers.Insert(customer).ConfigureAwait(false);
            return new(customer);
        }
    }
}

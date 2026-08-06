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
                return new(Statuses.NotFound, $"Customer {customerId} does not exist");

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
                // No empty address is invented here: an address the customer has not given yet is
                // absent, and every rule PostalAddress declares would be broken by a blank one.
            };

            // The e-mail pattern and the name length are model rules, declared in the .d3 as validate
            // expressions. The store enforces them before it writes and the generated controller turns
            // the failure into a 400 with every broken field named - so this method does not repeat it.
            await _context.Customers.Insert(customer).ConfigureAwait(false);
            return new(customer);
        }
    }
}

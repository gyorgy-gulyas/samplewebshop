using CustomerManagement.Customers.Customer;
using ServiceKit.Net;

namespace CustomerManagement.Customers.Context.Implementations
{
    public class CustomerIF_v1 : ICustomerIF_v1
    {
        private readonly ICustomerService _service;

        public CustomerIF_v1(ICustomerService service)
        {
            _service = service;
        }

        async Task<Response<ICustomerIF_v1.CustomerDTO>> ICustomerIF_v1.getCustomer(CallingContext ctx, string customerId)
        {
            var found = await _service.getCustomer(ctx, customerId).ConfigureAwait(false);
            if (found.IsFailed())
                return new(found.Error);

            return new(ToDto(found.Value));
        }

        async Task<Response<ICustomerIF_v1.CustomerDTO>> ICustomerIF_v1.registerCustomer(CallingContext ctx, string name, string email)
        {
            var created = await _service.registerCustomer(ctx, name, email).ConfigureAwait(false);
            if (created.IsFailed())
                return new(created.Error);

            return new(ToDto(created.Value));
        }

        // The DTO deliberately carries no address and no phone number: those are marked @gdpr in the
        // model, and personal data does not reach the published surface without a reason to be there.
        private static ICustomerIF_v1.CustomerDTO ToDto(CustomerAccount customer)
        {
            return new ICustomerIF_v1.CustomerDTO()
            {
                id = customer.id,
                name = customer.name,
                email = customer.email,
                status = (ICustomerIF_v1.CustomerStatuses)customer.status,
            };
        }
    }
}

using CustomerManagement.Customers.Customer;
using PolyPersist;
using PolyPersist.Net.Context;

namespace CustomerManagement.Customers.Context.Implementations
{
    public class CustomerStoreContext : StoreContext
    {
        public readonly IDocumentCollection<CustomerAccount> Customers;

        public CustomerStoreContext(IStoreProvider storeProvider)
            : base(storeProvider)
        {
            Customers = base.GetOrCreateDocumentCollection<CustomerAccount>().Result;
        }
    }
}

using PolyPersist;
using PolyPersist.Net.Context;
using Sales.OrderManagement.Order;

namespace Sales.OrderManagement.Context.Implementations
{
    // Everything the order context stores, in one place. The collections are created on first use,
    // so a fresh environment needs no migration step to start.
    public class OrderStoreContext : StoreContext
    {
        public readonly IDocumentCollection<OrderHeader> Orders;

        public OrderStoreContext(IStoreProvider storeProvider)
            : base(storeProvider)
        {
            Orders = base.GetOrCreateDocumentCollection<OrderHeader>().Result;
        }
    }
}

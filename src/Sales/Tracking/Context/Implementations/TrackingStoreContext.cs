using PolyPersist;
using PolyPersist.Net.Context;
using Sales.Tracking.OrderTrackingEntry;

namespace Sales.Tracking.Context.Implementations
{
    // Everything the tracking context stores, in one place. The collection is created on first use,
    // so a fresh environment needs no migration step to start.
    public class TrackingStoreContext : StoreContext
    {
        public readonly IDocumentCollection<TrackingEntry> Entries;

        public TrackingStoreContext(IStoreProvider storeProvider)
            : base(storeProvider)
        {
            Entries = base.GetOrCreateDocumentCollection<TrackingEntry>().Result;
        }
    }
}

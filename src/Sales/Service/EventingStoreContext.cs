using PolyPersist;
using PolyPersist.Net.Context;
using ServiceKit.Net.Eventing.PolyPersistStores;

namespace Sales.Service
{
    /// <summary>
    /// Where the platform's own two collections live.
    ///
    /// They are built from the SAME IStoreProvider the order context uses, and that is the entire
    /// reason this class exists rather than the collections being created wherever. Atomicity is not
    /// a property of the outbox code; it is a property of the outbox sitting in the store the domain
    /// write also goes to. Move it elsewhere and the platform still delivers - but the window
    /// between "the order is saved" and "the fact is queued" becomes real, and nothing in the code
    /// would look any different.
    /// </summary>
    public class EventingStoreContext : StoreContext
    {
        public readonly IDocumentCollection<OutboxRecord> Outbox;
        public readonly IDocumentCollection<InboxRecord> Inbox;

        public EventingStoreContext(IStoreProvider storeProvider)
            : base(storeProvider)
        {
            Outbox = base.GetOrCreateDocumentCollection<OutboxRecord>().Result;
            Inbox = base.GetOrCreateDocumentCollection<InboxRecord>().Result;
        }
    }
}

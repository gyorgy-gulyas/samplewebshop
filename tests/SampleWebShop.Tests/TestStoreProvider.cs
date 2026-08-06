using PolyPersist;
using PolyPersist.Net.Core;
using PolyPersist.Net.DocumentStore.Memory;

namespace SampleWebShop.Tests
{
    // The whole point of the sample is that it runs with nothing installed, and its tests inherit
    // that: the in-memory document store is the same one the service itself falls back to when
    // Stores:Document is left unconfigured, so these tests exercise the production path and not a
    // fake written for them.
    public class TestStoreProvider : StoreProvider
    {
        protected override IDocumentStore GetDocumentStore()
        {
            return new Memory_DocumentStore("");
        }
    }
}

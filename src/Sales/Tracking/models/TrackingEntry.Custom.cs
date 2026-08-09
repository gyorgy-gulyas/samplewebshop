using PolyPersist;

namespace Sales.Tracking.OrderTrackingEntry
{
    // The generated half carries the business fields; this half says how the entry is stored.
    public partial class TrackingEntry : IDocument
    {
        // Tracking entries are read one order at a time - "where is my order" is the only question
        // anybody asks - so they partition by the order they are about, not by their own id. That is
        // a storage decision and therefore lives here, not in the .d3 model.
        string IEntity.PartitionKey { get => order; set => order = value; }

        public string PartitionKey
        {
            get => (this as IEntity).PartitionKey;
            private set => (this as IEntity).PartitionKey = value;
        }
    }
}

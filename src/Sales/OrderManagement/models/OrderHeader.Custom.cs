using PolyPersist;

namespace Sales.OrderManagement.Order
{
    // The generated half carries the business fields; this half says how the aggregate is stored.
    // An order is a document: it is read and written whole, by its own id.
    public partial class OrderHeader : IDocument
    {
        // The partition key is a scaling decision, not a business concept, so it never appears in
        // the .d3 model - it is bound here. An order partitions by its own id.
        string IEntity.PartitionKey { get => id; set => id = value; }

        public string PartitionKey
        {
            get => (this as IEntity).PartitionKey;
            private set => (this as IEntity).PartitionKey = value;
        }
    }
}

using PolyPersist;

namespace CustomerManagement.Customers.Customer
{
    public partial class CustomerAccount : IDocument
    {
        string IEntity.PartitionKey { get => id; set => id = value; }

        public string PartitionKey
        {
            get => (this as IEntity).PartitionKey;
            private set => (this as IEntity).PartitionKey = value;
        }
    }
}

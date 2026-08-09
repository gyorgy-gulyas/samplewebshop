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

        /// <summary>
        /// The model's <c>command place( customerId ) emits OrderPlaced</c>.
        ///
        /// The generated half declares it; without this body the project does not compile. The root
        /// is what enforces the invariant that makes the fact true, so the root is what writes it
        /// down. Nothing is sent from here: Record only remembers, and the repository moves the fact
        /// into the outbox inside the same save. The Record overload exists because the model said
        /// this command emits OrderPlaced - recording anything else would not compile either.
        /// </summary>
        public partial void place(string customerId)
        {
            // A placement that is not a change of state is not a placement. Saying so here rather
            // than in the service is the point of having a root at all: there is no path to
            // Released that skips this check.
            // Field-level validation is deliberately NOT repeated here: the store validates before
            // it writes and answers with a path per broken field. This is the one thing the store
            // cannot know - that placing twice is not the same as placing once.
            if (status != OrderStatuses.Draft)
                throw new InvalidOperationException($"Order '{id}' is {status} and cannot be placed again.");

            status = OrderStatuses.Released;

            Record(new OrderPlaced()
            {
                orderId = id,
                customerId = customerId,
                totalPrice = totalPrice,
            });
        }
    }
}

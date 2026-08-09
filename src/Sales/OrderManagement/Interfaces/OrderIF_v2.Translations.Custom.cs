namespace Sales.OrderManagement
{
    // The same internal fact, translated a second time for a second audience.
    //
    // This is what a version of a published event actually costs and actually buys: v1's promise is
    // untouched - its subscribers still get orderId and totalAmount - while v2 says something
    // different about the very same OrderPlaced. Two translations, one domain event, no branch in
    // the aggregate.
    public static partial class OrderIF_v2Translations
    {
        public static partial IOrderIF_v2.OrderPlaced_v2 ToOrderPlaced_v2(Order.OrderPlaced @event)
        {
            return new IOrderIF_v2.OrderPlaced_v2()
            {
                orderId = @event.orderId,
                customerId = @event.customerId,
            };
        }
    }
}

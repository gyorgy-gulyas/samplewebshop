namespace Sales.OrderManagement
{
    // The same internal fact, translated a second time for a second audience.
    //
    // This is what a version of a published event actually costs and actually buys: v1's promise is
    // untouched - its subscribers still get orderId and totalAmount - while v2 says MORE about the
    // very same OrderPlaced. Two translations, one domain event, no branch in the aggregate.
    //
    // v2 keeps everything v1 said and adds to it, and that is enforced rather than intended: the
    // evolution lint refuses a higher version that drops a field, because then the number promised
    // nothing to whoever moved forward.
    public static partial class OrderIF_v2Translations
    {
        public static partial IOrderIF_v2.OrderPlaced_v2 ToOrderPlaced_v2(Order.OrderPlaced @event)
        {
            return new IOrderIF_v2.OrderPlaced_v2()
            {
                orderId = @event.orderId,
                totalAmount = @event.totalPrice,
                customerId = @event.customerId,
            };
        }
    }
}

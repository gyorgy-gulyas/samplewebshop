namespace Sales.OrderManagement
{
    // The other half of the generated translation. The emitter declared it and left it empty on
    // purpose: it will not match fields by name, because the published language is not the internal
    // one and name-matching would let the domain leak into the contract one rename at a time.
    //
    // What the outside world gets is decided here, in one readable place:
    //   - customerId stays inside. Who ordered is the order context's business; a subscriber that
    //     needs it asks, and then it is an authorisation decision instead of an accident.
    //   - totalPrice becomes totalAmount, because that is what the outside world calls it.
    public static partial class OrderIF_v1Translations
    {
        public static partial IOrderIF_v1.OrderPlaced_v1 ToOrderPlaced_v1(Order.OrderPlaced @event)
        {
            return new IOrderIF_v1.OrderPlaced_v1()
            {
                orderId = @event.orderId,
                totalAmount = @event.totalPrice,
            };
        }
    }
}

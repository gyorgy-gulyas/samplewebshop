using Microsoft.Extensions.Logging;
using PolyPersist.Net.Extensions;
using Sales.OrderManagement.Order;
using ServiceKit.Net;

namespace Sales.OrderManagement.Context.Implementations
{
    // The application service: it loads, lets the aggregate decide, saves, and reports. The generated
    // IOrderService says what it has to answer; the shape of the answer - Response with a status the
    // transports understand - comes from the platform.
    public class OrderService : IOrderService
    {
        private readonly OrderStoreContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(OrderStoreContext context, ILogger<OrderService> logger)
        {
            _context = context;
            _logger = logger;
        }

        async Task<Response<OrderHeader>> IOrderService.getOrder(CallingContext ctx, string orderId)
        {
            var order = await _context.Orders.Find(orderId, orderId).ConfigureAwait(false);
            if (order == null)
                return new(Statuses.NotFound, $"Order '{orderId}' does not exist");

            return new(order);
        }

        async Task<Response<OrderHeader>> IOrderService.placeOrder(CallingContext ctx, OrderHeader order)
        {
            // No hand-written validation here: the store validates before it writes, and the generated
            // controller turns the resulting ValidationExeption into a 400 carrying every broken field
            // with its path. Checking it here as well would only produce a second, poorer answer -
            // one sentence instead of a list a form can bind to.
            order.id = string.IsNullOrEmpty(order.id) ? Guid.NewGuid().ToString() : order.id;
            order.status = OrderStatuses.Released;

            await _context.Orders.Insert(order).ConfigureAwait(false);

            _logger?.LogInformation("Order {OrderId} placed by {IdentityId}", order.id, ctx?.IdentityId);

            return new(order);
        }

        Task<bool> IOrderService.handleOrderPlaced(CallingContext ctx, IOrderIF_v1.OrderPlaced_v1 @event)
        {
            _logger?.LogInformation("OrderPlaced handled for {OrderId}", @event?.orderId);
            return Task.FromResult(true);
        }
    }
}

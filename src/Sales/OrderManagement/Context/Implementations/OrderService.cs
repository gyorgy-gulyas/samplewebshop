using Microsoft.Extensions.Logging;
using PolyPersist.Net.Common;
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
            // A span around the work rather than around the request: the platform already timed the
            // request. This one says how long the STORE took, which is the part that varies.
            using (var activity = SalesTelemetry.StartActivity("load order"))
            {
                activity?.SetTag("sales.order.id", orderId);

                var order = await _context.Orders.Find(orderId, orderId).ConfigureAwait(false);
                if (order == null)
                {
                    activity?.SetTag("sales.order.found", false);
                    _logger?.LogInformation("Order {OrderId} was asked for and does not exist", orderId);
                    return new(Statuses.NotFound, $"Order '{orderId}' does not exist");
                }

                activity?.SetTag("sales.order.found", true);
                activity?.SetTag("sales.order.items", order.items?.Count ?? 0);
                return new(order);
            }
        }

        async Task<Response<OrderHeader>> IOrderService.placeOrder(CallingContext ctx, OrderHeader order)
        {
            using (var activity = SalesTelemetry.StartActivity("place order"))
            {
                // No hand-written validation here: the store validates before it writes, and the
                // generated controller turns the resulting ValidationExeption into a 400 carrying
                // every broken field with its path. Checking it here as well would only produce a
                // second, poorer answer - one sentence instead of a list a form can bind to.
                order.id = string.IsNullOrEmpty(order.id) ? Guid.NewGuid().ToString() : order.id;
                order.status = OrderStatuses.Released;

                activity?.SetTag("sales.order.id", order.id);
                activity?.SetTag("sales.order.items", order.items?.Count ?? 0);

                try
                {
                    await _context.Orders.Insert(order).ConfigureAwait(false);
                }
                catch (ValidationExeption validation)
                {
                    // Counted, not swallowed: the exception goes on to the generated controller,
                    // which is what turns it into a 400 with a path per broken field. What happens
                    // here is only that somebody watching can see how much of the traffic is a form
                    // filled in wrong - a number that says "fix the form", not "fix the service".
                    SalesTelemetry.OrdersRejected.Add(1, new KeyValuePair<string, object>("reason", "validation"));
                    activity?.SetTag("sales.order.rejected", "validation");
                    activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, "validation");

                    // The paths, not the values: which field was wrong is diagnosis, what was in it
                    // is the customer's business.
                    _logger?.LogWarning("Order {OrderId} was refused, {ErrorCount} broken field(s): {Fields}",
                        order.id,
                        validation.ValidationErrors.Count,
                        string.Join(", ", validation.ValidationErrors.Select(error => error.Path)));

                    throw;
                }

                SalesTelemetry.OrdersPlaced.Add(1);
                SalesTelemetry.OrderValue.Record((double)order.totalPrice);

                // The identity comes from the calling context, which the platform filled from the
                // request; it is on every line of this request anyway, and repeating it here is what
                // makes THIS line answer "who placed it" on its own.
                _logger?.LogInformation("Order {OrderId} placed by {IdentityId}, {ItemCount} item(s), total {TotalPrice}",
                    order.id, ctx?.IdentityId, order.items?.Count ?? 0, order.totalPrice);

                return new(order);
            }
        }

        Task<bool> IOrderService.handleOrderPlaced(CallingContext ctx, IOrderIF_v1.OrderPlaced_v1 @event)
        {
            _logger?.LogInformation("OrderPlaced handled for {OrderId}", @event?.orderId);
            return Task.FromResult(true);
        }
    }
}

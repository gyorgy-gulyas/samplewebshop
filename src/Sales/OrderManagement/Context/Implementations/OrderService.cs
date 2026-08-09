using Microsoft.Extensions.Logging;
using PolyPersist.Net.Common;
using PolyPersist.Net.Extensions;
using PolyPersist.Net.Transactions;
using Sales.OrderManagement.Order;
using ServiceKit.Net;
using ServiceKit.Net.Eventing;
using ServiceKit.Net.Eventing.PolyPersistStores;

namespace Sales.OrderManagement.Context.Implementations
{
    // The application service: it loads, lets the aggregate decide, saves, and reports. The generated
    // IOrderService says what it has to answer; the shape of the answer - Response with a status the
    // transports understand - comes from the platform.
    public class OrderService : IOrderService
    {
        private readonly OrderStoreContext _context;
        private readonly IOutboxStore _outbox;
        private readonly IEventRecorder _recorder;
        private readonly ILogger<OrderService> _logger;

        public OrderService(OrderStoreContext context, IOutboxStore outbox, IEventRecorder recorder, ILogger<OrderService> logger)
        {
            _context = context;
            _outbox = outbox;
            _recorder = recorder;
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

                // The service does not decide that this is a placement and it does not announce it.
                // The root changes its own state and writes the fact down; nothing has left the
                // process yet.
                order.place(order.customer);

                activity?.SetTag("sales.order.id", order.id);
                activity?.SetTag("sales.order.items", order.items?.Count ?? 0);

                // One unit of work for the state AND the fact. WithOutbox is the whole difference:
                // the transaction drains what the root recorded and queues it into the outbox as
                // part of this same commit, so there is no window where the order exists and
                // nobody was told - or where the world reacts to an order that was rolled back.
                var unitOfWork = new Transaction();
                var transaction = unitOfWork.WithOutbox(_outbox, _recorder);

                try
                {
                    await transaction.Insert(_context.Orders, order).ConfigureAwait(false);
                    await transaction.Commit().ConfigureAwait(false);
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

        // The reaction to OrderPlaced used to live here, as a method on this service that nothing
        // ever called. It is now where the model puts it: on the Tracking context, in a generated
        // handler the platform registers and delivers to - see Sales.Tracking.OnOrderPlacedHandler.
    }
}

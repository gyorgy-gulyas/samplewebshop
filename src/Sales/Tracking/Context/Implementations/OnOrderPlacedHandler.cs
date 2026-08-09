using Microsoft.Extensions.Logging;
using Sales.OrderManagement;
using Sales.Tracking.Context.Implementations;
using Sales.Tracking.OrderTrackingEntry;
using ServiceKit.Net.Eventing;

namespace Sales.Tracking
{
    // The other half of the generated handler: the reaction itself.
    //
    // Note what this class does NOT have. No reference to OrderManagement's context or service, no
    // client, no call. It knows one published contract type and nothing else about the other side -
    // which is why OrderManagement can rename customerId tomorrow and this file will not notice.
    public sealed partial class OnOrderPlacedHandler
    {
        private readonly TrackingStoreContext _context;
        private readonly ILogger<OnOrderPlacedHandler> _logger;

        // The generated half declares no constructor, so this one is the class's constructor. The
        // platform resolves the handler from the container per delivery, exactly as it would resolve
        // a request-scoped service.
        public OnOrderPlacedHandler(TrackingStoreContext context, ILogger<OnOrderPlacedHandler> logger = null)
        {
            _context = context;
            _logger = logger;
        }

        private async partial Task onOrderPlaced(EventContext context, IOrderIF_v1.OrderPlaced_v1 @event, CancellationToken cancellationToken)
        {
            // Delivery is at-least-once, and the platform's inbox already drops the repeat for this
            // consumer group. The entry id is derived from the delivery anyway, so a redelivery that
            // somehow got past the inbox would collide instead of writing a second Pending row.
            var entry = new TrackingEntry()
            {
                id = context.EventId,
                order = @event.orderId,
                trackingStatus = TrackingStatuses.Pending,
                statusDate = context.OccurredAt.UtcDateTime,
            };

            await _context.Entries.Insert(entry).ConfigureAwait(false);

            // The causation id is the fact that caused this one. It is what turns a pile of events
            // into a chain somebody can walk backwards from a customer complaint.
            _logger?.LogInformation(
                "Tracking opened for order {OrderId}, caused by {CausationId}",
                @event.orderId, context.EventId);
        }
    }
}

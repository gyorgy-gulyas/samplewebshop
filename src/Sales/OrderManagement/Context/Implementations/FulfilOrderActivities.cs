using Microsoft.Extensions.Logging;
using PolyPersist.Net.Core;
using Sales.OrderManagement.Order;

namespace Sales.OrderManagement.Context.Implementations
{
    // The activities are the only place in the workflow that touches the outside world - the
    // warehouse, the payment provider, the courier. They are ordinary async methods: nothing here
    // knows about Temporal, and the sample can therefore stand them up in memory.
    //
    // Every one of them must be idempotent enough to survive a retry, because the platform WILL
    // retry them: @retry( 3 ) on the workflow, and @retry( 1 ) on the card charge exactly because
    // that one is not.
    public class FulfilOrderActivities : IFulfilOrderActivities
    {
        private readonly ILogger<FulfilOrderActivities> _logger;

        public FulfilOrderActivities(ILogger<FulfilOrderActivities> logger)
        {
            _logger = logger;
        }

        Task<string> IFulfilOrderActivities.reserveStock(EntityId<OrderHeader> order, string sku, decimal quantity)
        {
            var reservationId = Guid.NewGuid().ToString();
            _logger?.LogInformation("Reserved {Quantity} of {Sku} for order {OrderId}, reservation {ReservationId}", quantity, sku, (string)order, reservationId);
            return Task.FromResult(reservationId);
        }

        Task IFulfilOrderActivities.releaseStock(EntityId<OrderHeader> order, string sku, string reservationId)
        {
            // The reservation id was not passed in by hand: the model says reserveStock is
            // compensated by releaseStock, and the generated facade bound the argument from what
            // reserveStock returned.
            _logger?.LogWarning("Releasing reservation {ReservationId} of {Sku} for order {OrderId}", reservationId, sku, (string)order);
            return Task.CompletedTask;
        }

        Task<string> IFulfilOrderActivities.chargeCard(EntityId<OrderHeader> order, decimal amount)
        {
            var chargeId = Guid.NewGuid().ToString();
            _logger?.LogInformation("Charged {Amount} for order {OrderId}, charge {ChargeId}", amount, (string)order, chargeId);
            return Task.FromResult(chargeId);
        }

        Task IFulfilOrderActivities.refundCard(EntityId<OrderHeader> order, string chargeId)
        {
            _logger?.LogWarning("Refunding charge {ChargeId} for order {OrderId}", chargeId, (string)order);
            return Task.CompletedTask;
        }

        Task<string> IFulfilOrderActivities.shipOrder(EntityId<OrderHeader> order, string shippingAddress)
        {
            var trackingNumber = $"TRK-{Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant()}";
            _logger?.LogInformation("Shipped order {OrderId} to {Address}, tracking {TrackingNumber}", (string)order, shippingAddress, trackingNumber);
            return Task.FromResult(trackingNumber);
        }
    }
}

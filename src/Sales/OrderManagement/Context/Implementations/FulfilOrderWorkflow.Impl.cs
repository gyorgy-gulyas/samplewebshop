using PolyPersist.Net.Core;
using Sales.OrderManagement.Order;

namespace Sales.OrderManagement
{
    // The other half of the generated workflow: the ORDER of the steps, and nothing else. There is
    // no rollback bookkeeping here on purpose - calling a step through Steps records its
    // compensation, and the generated run wrapper replays them in reverse if anything throws.
    public partial class FulfilOrderWorkflow
    {
        private string _status = "pending";
        private string _cancelReason;

        private async partial Task<string> OnFulfil(EntityId<OrderHeader> order, string sku, decimal quantity, decimal amount, string shippingAddress)
        {
            _status = "reserving stock";
            await Steps.reserveStock(order, sku, quantity);

            // A cancellation that arrived while the stock was being reserved is honoured here.
            // Throwing is what triggers the rollback: the reservation is released for us.
            if (_cancelReason != null)
                throw new ApplicationException($"The order was cancelled: {_cancelReason}");

            _status = "charging";
            await Steps.chargeCard(order, amount);

            _status = "shipping";
            var trackingNumber = await Steps.shipOrder(order, shippingAddress);

            _status = "shipped";
            return trackingNumber;
        }

        private partial Task OnCancel(string reason)
        {
            // A signal only records the intent; the run body decides what to do with it, because a
            // step that is already running cannot be interrupted halfway.
            _cancelReason = reason;
            return Task.CompletedTask;
        }

        private partial string OnStatus() => _status;
    }
}

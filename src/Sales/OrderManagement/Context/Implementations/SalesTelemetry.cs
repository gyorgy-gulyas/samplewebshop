using System.Diagnostics;
using System.Diagnostics.Metrics;
using ServiceKit.Net;

namespace Sales.OrderManagement.Context.Implementations
{
    // What this context is worth measuring by, in business terms rather than in HTTP terms.
    //
    // The platform already counts requests and times them; that says whether the service is healthy.
    // It cannot say whether orders are being placed, how large they are, or how much of the traffic
    // is a customer filling the form in wrong - and those are the numbers somebody actually watches.
    //
    // The instruments come off ServiceKitDiagnostics.Meter and the spans off its ActivitySource,
    // because those are the names the host registers with OpenTelemetry. A meter of our own would
    // record into nothing.
    internal static class SalesTelemetry
    {
        // Created once and kept: an instrument built per call would create a new time series every
        // time.
        internal static readonly Counter<long> OrdersPlaced =
            ServiceKitDiagnostics.Meter.CreateCounter<long>(
                "sales.orders.placed",
                unit: "{order}",
                description: "Orders accepted and stored.");

        internal static readonly Counter<long> OrdersRejected =
            ServiceKitDiagnostics.Meter.CreateCounter<long>(
                "sales.orders.rejected",
                unit: "{order}",
                description: "Orders refused before they were stored, by reason.");

        internal static readonly Histogram<double> OrderValue =
            ServiceKitDiagnostics.Meter.CreateHistogram<double>(
                "sales.order.value",
                unit: "HUF",
                description: "The total of an accepted order.");

        internal static Activity StartActivity(string name)
        {
            return ServiceKitDiagnostics.ActivitySource.StartActivity(name);
        }
    }
}

using System.Diagnostics;
using System.Net.Http;
using Sales.OrderManagement;
using ServiceKit.Net;

namespace SampleWebShop.Tests
{
    // What the sample is supposed to demonstrate: that an order can be followed afterwards.
    //
    // These tests watch the real host from outside - the spans through a listener on the platform's
    // ActivitySource, the numbers by scraping the endpoint the host serves. Neither needs a
    // collector, which is the claim being made.
    [TestClass]
    public class ObservabilityTests
    {
        private static ActivityListener _listener;
        private static readonly List<Activity> _spans = new List<Activity>();
        private static readonly object _lock = new object();

        private IOrderIF_v1 _client;
        private HttpClient _http;

        [ClassInitialize]
        public static void Listen(TestContext context)
        {
            _listener = new ActivityListener()
            {
                ShouldListenTo = source => source.Name == ServiceKitDiagnostics.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    lock (_lock)
                        _spans.Add(activity);
                },
            };

            ActivitySource.AddActivityListener(_listener);
        }

        [ClassCleanup]
        public static void StopListening()
        {
            _listener?.Dispose();
        }

        [TestInitialize]
        public void Setup()
        {
            _client = new OrderIF_v1_RestClient(ServiceHostFixture.Clients);
            _http = new HttpClient() { BaseAddress = new Uri(ServiceHostFixture.RestAddress) };

            lock (_lock)
                _spans.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _http?.Dispose();
        }

        private static IReadOnlyList<Activity> Spans
        {
            get
            {
                lock (_lock)
                    return _spans.ToList();
            }
        }

        private static IOrderIF_v1.OrderDTO AnOrder(decimal total, params decimal[] quantities)
        {
            return new IOrderIF_v1.OrderDTO()
            {
                orderingDate = "2026-08-06",
                orderStatus = IOrderIF_v1.OrderStatuses.Draft,
                totalPrice = total,
                customerData = new IOrderIF_v1.OrderDTO.CustomerDataDTO() { customerId = "customer-1", customerName = "Kis Béla" },
                items = quantities.Select((quantity, index) => new IOrderIF_v1.OrderItemDTO()
                {
                    productId = $"p{index}",
                    productName = $"product {index}",
                    quantity = quantity,
                    unitPrice = 10,
                    subTotalPrice = 10 * quantity,
                }).ToList(),
            };
        }

        private Task<string> Scrape()
        {
            return _http.GetStringAsync("/metrics");
        }

        [TestMethod]
        public async Task Placing_an_order_leaves_a_span_that_names_the_order()
        {
            var placed = await _client.placeOrder(new CallingContext(), AnOrder(20, 1, 1));
            Assert.IsTrue(placed.IsSuccess());

            var span = Spans.SingleOrDefault(s => s.DisplayName == "place order");

            Assert.IsNotNull(span, "spans seen: " + string.Join(" | ", Spans.Select(s => s.DisplayName)));
            Assert.AreEqual(placed.Value.id, span.GetTagItem("sales.order.id"));
            Assert.AreEqual(2, span.GetTagItem("sales.order.items"));
        }

        [TestMethod]
        public async Task The_span_hangs_under_the_request_it_belongs_to()
        {
            // This is what makes a trace a trace rather than a pile of spans: the work is a child of
            // the call that asked for it, so one order is one tree.
            var placed = await _client.placeOrder(new CallingContext(), AnOrder(10, 1));
            Assert.IsTrue(placed.IsSuccess());

            var span = Spans.Single(s => s.DisplayName == "place order");

            Assert.AreNotEqual(default(ActivitySpanId), span.ParentSpanId);
            Assert.AreNotEqual(default(ActivityTraceId), span.TraceId);
        }

        [TestMethod]
        public async Task A_refused_order_marks_its_span_and_says_why()
        {
            var refused = await _client.placeOrder(new CallingContext(), AnOrder(10, 1, 0));
            Assert.AreEqual(Statuses.BadRequest, refused.Status);

            var span = Spans.Single(s => s.DisplayName == "place order");

            Assert.AreEqual("validation", span.GetTagItem("sales.order.rejected"));
            Assert.AreEqual(ActivityStatusCode.Error, span.Status);
        }

        [TestMethod]
        public async Task Reading_an_order_that_is_not_there_says_so_on_the_span()
        {
            await _client.getOrder(new CallingContext(), "no-such-order");

            var span = Spans.Single(s => s.DisplayName == "load order");

            Assert.AreEqual(false, span.GetTagItem("sales.order.found"));
        }

        [TestMethod]
        public async Task Orders_are_counted_in_business_terms_and_not_only_as_requests()
        {
            // The platform already counts requests; that says whether the service is healthy. It
            // cannot say whether orders are being placed.
            await _client.placeOrder(new CallingContext(), AnOrder(20, 1, 1));

            var scraped = await Scrape();

            StringAssert.Contains(scraped, "sales_orders_placed_total");
        }

        [TestMethod]
        public async Task A_form_filled_in_wrong_is_counted_apart_from_a_broken_service()
        {
            await _client.placeOrder(new CallingContext(), AnOrder(10, 0));

            var scraped = await Scrape();

            StringAssert.Contains(scraped, "sales_orders_rejected_total");
            StringAssert.Contains(scraped, "reason=\"validation\"");
        }

        [TestMethod]
        public async Task What_an_order_is_worth_is_recorded_as_a_distribution()
        {
            // A total is not an average: the histogram is what answers "how big is a big order".
            await _client.placeOrder(new CallingContext(), AnOrder(1234, 1));

            var scraped = await Scrape();

            StringAssert.Contains(scraped, "sales_order_value_HUF_sum");
            StringAssert.Contains(scraped, "sales_order_value_HUF_count");
        }

        [TestMethod]
        public async Task The_saga_steps_are_spans_of_their_own()
        {
            // Only the trace says how long each step took and which was the slow one - and a
            // compensation shows up beside the step it undid.
            var activities = (IFulfilOrderActivities)new Sales.OrderManagement.Context.Implementations.FulfilOrderActivities(null);

            var reservationId = await activities.reserveStock("order-1", "sku-1", 2);
            await activities.chargeCard("order-1", 100);
            await activities.releaseStock("order-1", "sku-1", reservationId);

            var names = Spans.Select(s => s.DisplayName).ToList();
            CollectionAssert.IsSubsetOf(new[] { "reserve stock", "charge card", "release stock (compensation)" }, names);

            var compensation = Spans.Single(s => s.DisplayName == "release stock (compensation)");
            Assert.AreEqual(true, compensation.GetTagItem("sales.compensation"));
            Assert.AreEqual(reservationId, compensation.GetTagItem("sales.reservation.id"));
        }

        [TestMethod]
        public async Task Nothing_personal_is_put_on_a_span()
        {
            // The tags are diagnosis, not a copy of the customer's data. A shipping address is where
            // somebody lives; a trace store is not the place for it.
            var activities = (IFulfilOrderActivities)new Sales.OrderManagement.Context.Implementations.FulfilOrderActivities(null);

            await activities.shipOrder("order-1", "6720 Szeged, Kárász utca 1.");

            var span = Spans.Single(s => s.DisplayName == "ship order");
            var tagValues = span.TagObjects.Select(tag => tag.Value?.ToString() ?? string.Empty).ToList();

            Assert.IsFalse(tagValues.Any(value => value.Contains("Kárász")), string.Join(" | ", tagValues));
        }
    }
}

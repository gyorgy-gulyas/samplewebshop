# samplewebshop

The MicronIQ platform's own test bed. Everything here exists to be run, poked at and broken — if a
platform capability cannot be demonstrated on this sample, that is a finding about the platform.

## What it already shows

| Capability | Where |
|---|---|
| `.d3` model → generated code | `src/*.d3`, everything under `src/**/Models`, `Interfaces`, `Context/Controllers`, `InternalClient` |
| Composite inheritance | `Core.Base.BaseEntity`, `Sales.OrderManagement.SalesDocument` |
| Cross-aggregate reference | `OrderHeader.customer : ref CustomerManagement.Customers.Customer` → `EntityId<CustomerAccount>` |
| Field validation | `validate` rules on `OrderItem`, `CustomerAccount`, `PostalAddress` → generated `IValidable.Validate` |
| Optionality and personal data | `@optional`, `@gdpr` on `CustomerAccount` |
| Published surface versioning | `OrderIF` v1 and v2 side by side, answering from one service |
| REST + gRPC + BFF client | generated controllers, `InternalClient`, `ApiClientKit`, `src/BFF` TypeScript |
| PolyPersist | `OrderStoreContext`, `CustomerStoreContext`, document collections |
| Service host | `BaseServiceHost` with health probes, CORS and Swagger |
| Observability | structured logs, spans and business metrics — see [Following one order afterwards](#following-one-order-afterwards) |
| Eventing | `OrderHeader.place` records `OrderPlaced`, the commit queues it, and the **Tracking** context reacts to the published `OrderIF.v1.OrderPlaced.v1` — outbox, relay, broker, inbox, all from the model |

## Running it

The MicronIQ packages come from a local feed built from the sibling repositories, so build those
first into `../LocalDevNugetFeed` (see `nuget.config`). Then:

    dotnet build samplewebshop.sln
    dotnet run --project src/Sales/Service/Sales.Service.csproj

Storage is in-memory unless `Stores:Document` is configured, so nothing else has to be installed to
try it. Health probes are at `/sales/health/live` and `/sales/health/ready`.

    curl -X POST http://localhost:5000/sales/sales/ordermanagement/orderif/v1/placeorder \
      -H "Content-Type: application/json" \
      -d '{"orderingDate":"2026-08-04","orderStatus":"Draft","totalPrice":100,
           "customerData":{"customerId":"cust-1","customerName":"Test"},
           "items":[{"productId":"p1","productName":"Widget","quantity":2,
                     "unitPrice":50,"subTotalPrice":100,"deliveryStatus":"NotDelivered"}]}'

## Regenerating from the model

The generated code is committed so the repository is browsable, but it is owned by the emitter and
overwritten on every run. After changing a `.d3`:

    python -m d3i -i <repo>/src/webshop.d3 -e dotnet:backend -o <repo>/src
    python -m d3i -i <repo>/src/webshop.d3 -e proto -o <repo>/src
    python -m d3i -i <repo>/src/webshop.d3 -e typescript:client -o <repo>/src

Hand-written code lives in `Context/Implementations` and `Service`, plus the `*.Custom.cs` partials
that bind the aggregates to storage. On the client side it is `src/BFF/api/BFFRestClient.ts` and
`src/BFF/api/ApiError.ts`. The emitter never writes those.

The generated TypeScript is checked the same way the generated C# is compiled — nothing had ever
type-checked it, which is how three separate pieces of broken codegen went unnoticed:

    cd src/BFF && npm install && npm run typecheck

## Tests

    dotnet test tests/SampleWebShop.Tests/SampleWebShop.Tests.csproj

They need nothing installed — no Docker, no database, no Temporal — because the sample does not
either. This is also where the sample shows how a MicronIQ application is tested:

| Layer | What it pins down |
|---|---|
| `OrderServiceTests`, `CustomerServiceTests` | the application service over a real in-memory store: what is written, what is refused, and with which path |
| `OrderSurfaceTests` | the published surfaces map and nothing more — v1 and v2 answering from one service, a failure keeping its status |
| `ValidationPathTests` | the DTO validator and the domain validator name the same field the same way, so a form can bind what it is shown |
| `FulfilOrderTests` | the saga: the rollback order, the compensation arguments, and the retry and deadline the model declared |
| `OrderRestContractTests`, `OrderGrpcContractTests` | the real host, started in process, called through the **generated** clients over both transports |
| `EventingChainTests` | the whole chain in the real host: place an order in one context, wait for another to react — root, outbox, relay, broker, dispatcher, generated publisher, generated handler |

The contract tests are the ones that earn their keep. Everything they touch between the client call
and the service — routing, the JSON body, the status mapping, the error list — is code nobody wrote
by hand, and until a test sent a request over it none of it had ever run. The first run found that
the generated .NET REST client could not be constructed, addressed, or understood by its own server,
and that the gRPC surface was never mapped at all.

A generated client no longer takes an address at all: it takes the client factory and asks it for
the service it calls, by name. So the contract tests construct their clients exactly the way the
running service would — the fixture puts the ports it happened to get under `Services:<name>` and
hands over a real factory. The addresses left the generated code, and with them went the hand-made
`HttpClient` that holds its connections open through a DNS change and the per-call-site gRPC channel
that turns into a connection storm — both of which work perfectly until there is traffic.

`EventingChainTests` earns its keep the same way, one layer further in. Every piece of the eventing
path already had unit tests of its own, and every one of them would still pass with the chain broken
in the middle — a translation nobody runs, a handler nobody registers, an outbox nobody drains all
look exactly like working code from the inside. So the test places an order through the published
surface and then waits for a **different context** to have reacted, touching nothing in between.
Writing it is what showed that the emitter's `Events/` output belonged to no project at all: the
generated domain event did not compile into anything.

The test host listens on two ports, and that is not a test convenience: without TLS there is no
ALPN, so one cleartext port cannot serve both HTTP/1.1 (REST) and HTTP/2 (gRPC). The running sample
has a single cleartext port, so its gRPC surface is reachable only when TLS is configured or a
second HTTP/2 endpoint is added.

## Following one order afterwards

The platform gives every request an identity and hands it to the log and to the trace alike; the
sample's job is to put something worth reading under it.

**Logs.** Every line of a request carries `CorrelationId`, and the correlation id a request is given
is the **trace id** of its span — so one value finds the log lines and the trace. It is answered in
the `correlation-id` response header too, which is what a support call can quote:

    curl -si -X POST http://localhost:5000/sales/ordermanagement/orderif/v1/placeorder ... | grep correlation-id
    # correlation-id: 10ef74c1f0917cc97b5b9790426879ec

    # every line of that one call, and nothing else:
    dotnet run --project src/Sales/Service | grep 10ef74c1f0917cc97b5b9790426879ec

`appsettings.json` sets the levels and the sink; `appsettings.Development.json` swaps the JSON for
something a person reads. Neither is required — with no configuration at all the host still logs
one JSON object per line.

**Traces.** The service opens spans of its own around the work that varies: `place order`, `load
order`, and one per saga step, with the compensations named as such. The tags are diagnosis — order
id, item count, reservation and charge ids — and deliberately not a copy of the customer's data: a
shipping address is where somebody lives, and a trace store is not the place for it.

**Metrics.** `/metrics` is served by the host. Beside the platform's request rate and duration, this
context counts what somebody actually watches:

| | |
|---|---|
| `sales_orders_placed_total` | orders accepted and stored |
| `sales_orders_rejected_total{reason="validation"}` | a form filled in wrong — which says *fix the form*, not *fix the service* |
| `sales_order_value_HUF` | what an order is worth, as a distribution rather than an average |

**None of this needs anything installed.** The spans are created and never exported, their trace id
still reaches every log line, and the scrape endpoint is the service's own. To *look* at them:

    docker compose -f deploy/observability/docker-compose.yml up -d
    OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project src/Sales/Service

Traces at <http://localhost:16686>, metrics at <http://localhost:9090>.

## What is still missing

Tracked on the `SampleWebShop` tab of `docs/MicronIQ-Roadmap.xlsx`. The big ones: the audit trail,
the other seven PolyPersist storage models, structured logging and metrics, and an ACL over a
payment provider.

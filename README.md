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

The contract tests are the ones that earn their keep. Everything they touch between the client call
and the service — routing, the JSON body, the status mapping, the error list — is code nobody wrote
by hand, and until a test sent a request over it none of it had ever run. The first run found that
the generated .NET REST client could not be constructed, addressed, or understood by its own server,
and that the gRPC surface was never mapped at all.

The test host listens on two ports, and that is not a test convenience: without TLS there is no
ALPN, so one cleartext port cannot serve both HTTP/1.1 (REST) and HTTP/2 (gRPC). The running sample
has a single cleartext port, so its gRPC surface is reachable only when TLS is configured or a
second HTTP/2 endpoint is added.

## What is still missing

Tracked on the `SampleWebShop` tab of `docs/MicronIQ-Roadmap.xlsx`. The big ones: the audit trail,
the other seven PolyPersist storage models, structured logging and metrics, and an ACL over a
payment provider.

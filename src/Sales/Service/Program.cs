using PolyPersist;
using PolyPersist.Net.Context;
using PolyPersist.Net.Core;
using PolyPersist.Net.DocumentStore.Memory;
using PolyPersist.Net.DocumentStore.MongoDB;
using Sales.OrderManagement;
using Sales.OrderManagement.Context.Implementations;
using Sales.Service;
using Sales.Tracking.Context.Implementations;
using ServiceKit.Net;
using ServiceKit.Net.Eventing;
using ServiceKit.Net.Eventing.PolyPersistStores;

BaseServiceHost.Create<SalesServiceHost>(args, SalesServiceHost.DefaultOptions).Run();

public class SalesServiceHost : BaseServiceHost
{
    // The host options live here rather than inline in the startup line so that a test can stand
    // the service up exactly as production does. A test that re-declares them tests a host nobody
    // runs.
    public static BaseServiceHost.Options DefaultOptions => new()
    {
        // Off for now: the sample has no identity provider yet. Turn it on and the platform maps the
        // REST controllers behind RequireAuthorization on its own.
        WithAuthentication = false,
        WithGrpc = true,
        WithRest = true,
        WithReponseCompression = false,
        PathBase = "/sales"
    };


    protected override void _BeforeAddServices(IServiceCollection services, Options options)
    {
    }

    protected override void _AfterAddServices(IServiceCollection services, Options options)
    {
        services.AddSingleton<IStoreProvider>(provider => new SalesStoreProvider(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<OrderStoreContext>();
        services.AddSingleton<TrackingStoreContext>();

        // Scoped, not singleton, and the eventing is what forced it: the recorder a save drains is a
        // unit of work's pending list, and two requests sharing one would hand each other's facts to
        // whichever committed first. A request is the unit of work, so the service that runs it - and
        // the surfaces in front of it - live exactly as long.
        services.AddScoped<IOrderIF_v1, OrderIF_v1>();
        services.AddScoped<IOrderIF_v2, OrderIF_v2>();
        services.AddScoped<IOrderService, OrderService>();

        _AddEventing(services);
        _AddWorkflows(services);
    }

    // Everything that carries a fact from the aggregate that recorded it to the context that reacts
    // to it. Three lines of intent and one of storage - and NOT one line naming a handler, an event
    // or a channel: those come from the model, and a host that had to list them would be a host that
    // can be out of date with it.
    private void _AddEventing(IServiceCollection services)
    {
        // Registered before AddServiceKitEventing, whose own registration is a TryAdd: this is where
        // the sample says which service produced a fact. The correlation, causation and tenant on the
        // envelope are filled by the platform.
        services.AddScoped(_ => new EventRecordingContext() { Source = "Sales.OrderManagement" });

        services.AddServiceKitEventing();

        // The outbox and the inbox live in the SAME store the domain writes to, which is what makes
        // "the order was saved" and "the fact was queued" one commit rather than two hopeful ones.
        services.UseEventing_PolyPersist(
            provider => provider.GetRequiredService<EventingStoreContext>().Outbox,
            provider => provider.GetRequiredService<EventingStoreContext>().Inbox);
        services.AddSingleton<EventingStoreContext>();

        // Fills in what is still missing - the broker and the dead-letter sink - and leaves the two
        // stores above alone, because every registration in it is a TryAdd. So the sample runs with
        // nothing installed, on a durable outbox, and a deployment replaces the broker without
        // touching a line above.
        services.UseEventing_InMemory();

        // No handler is named here. The generated ones carry [AutoRegisterEventHandler] and are found
        // by looking - the publishers in OrderManagement and the reaction in Tracking alike.
        services.AddEventHandlersFromAssemblies(
            typeof(OrderIF_v1).Assembly,
            typeof(Sales.Tracking.OnOrderPlacedHandler).Assembly);
    }

    // The order fulfilment saga needs a Temporal server, and the point of this sample is that it
    // runs with nothing installed - so the worker only starts once Temporal:TargetHost is
    // configured. The workflow, its activities and the generated registration are compiled either
    // way, so the saga cannot rot unnoticed while the worker is switched off.
    private void _AddWorkflows(IServiceCollection services)
    {
        var temporalHost = _builder.Configuration["Temporal:TargetHost"];
        if (string.IsNullOrWhiteSpace(temporalHost) == true)
            return;

        // The activity implementation is resolved from the container by the worker, so it has to be
        // registered against the GENERATED interface.
        services.AddSingleton<IFulfilOrderActivities, FulfilOrderActivities>();

        services.UseWorkflows(
            registry => FulfilOrderRegistration.Register(registry),
            options =>
            {
                options.TargetHost = temporalHost;
                options.Namespace = _builder.Configuration["Temporal:Namespace"] ?? "default";
            });
    }

    protected override void _BeforeBuild(WebApplication app, Options options)
    {
    }

    protected override void _AfterBuild(WebApplication app, Options options)
    {
    }

    protected override Task _BeforeRun(WebApplication app, Options options)
    {
        return Task.CompletedTask;
    }
}

namespace Sales.Service
{
    // Which storage engine backs which storage model is a deployment decision, so it lives in
    // configuration and never in the source. Unset means the in-memory store, which is what makes
    // the sample runnable with no Docker and no database at all.
    public class SalesStoreProvider : StoreProvider
    {
        private const string DocumentConnectionKey = "Stores:Document";

        private readonly IConfiguration _configuration;

        public SalesStoreProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override IDocumentStore GetDocumentStore()
        {
            var connectionString = _configuration[DocumentConnectionKey];
            if (string.IsNullOrWhiteSpace(connectionString) == true)
                return new Memory_DocumentStore("");

            return new MongoDB_DocumentStore(connectionString);
        }
    }
}

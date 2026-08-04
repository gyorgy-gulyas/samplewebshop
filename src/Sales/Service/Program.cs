using PolyPersist;
using PolyPersist.Net.Core;
using PolyPersist.Net.DocumentStore.Memory;
using PolyPersist.Net.DocumentStore.MongoDB;
using Sales.OrderManagement;
using Sales.OrderManagement.Context.Implementations;
using Sales.Service;
using ServiceKit.Net;

BaseServiceHost.Create<SalesServiceHost>(args, new BaseServiceHost.Options()
{
    // Off for now: the sample has no identity provider yet. Turn it on and the platform maps the
    // REST controllers behind RequireAuthorization on its own.
    WithAuthentication = false,
    WithGrpc = true,
    WithRest = true,
    WithReponseCompression = false,
    PathBase = "/sales"
}).Run();

public class SalesServiceHost : BaseServiceHost
{
    protected override void _BeforeAddServices(IServiceCollection services, Options options)
    {
    }

    protected override void _AfterAddServices(IServiceCollection services, Options options)
    {
        services.AddSingleton<IStoreProvider>(provider => new SalesStoreProvider(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<OrderStoreContext>();

        // the published surfaces
        services.AddSingleton<IOrderIF_v1, OrderIF_v1>();
        services.AddSingleton<IOrderIF_v2, OrderIF_v2>();
        // the application service behind them
        services.AddSingleton<IOrderService, OrderService>();

        _AddWorkflows(services);
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

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sales.OrderManagement;
using ServiceKit.Net;

namespace SampleWebShop.Tests
{
    // One host for the whole test assembly.
    //
    // It is the PRODUCTION host: the options come from SalesServiceHost.DefaultOptions, and with
    // Stores:Document unset the store provider falls back to memory - which is what makes the sample
    // runnable with no Docker, no database and no Temporal, and what makes these tests runnable
    // anywhere.
    //
    // It listens on TWO ports, and that is not a test convenience. Without TLS there is no ALPN, so
    // a cleartext port cannot negotiate between HTTP/1.1 and HTTP/2: Kestrel answers an h2c request
    // on an Http1AndHttp2 endpoint with HTTP_1_1_REQUIRED. REST needs HTTP/1.1 and gRPC needs
    // HTTP/2, so cleartext needs one port each. With TLS a single port serves both - which is why
    // the running sample (one cleartext port) can be called over REST and not over gRPC.
    [TestClass]
    public static class ServiceHostFixture
    {
        private static IHost _host;

        public static string RestAddress { get; private set; }
        public static string GrpcAddress { get; private set; }

        // The running host's container. What the eventing chain test needs: the outbox, the relay and
        // the subscriber are hosted services inside THIS host, so a test that built its own would be
        // watching a pipeline nobody runs.
        public static IServiceProvider Services => _host.Services;

        // Where the generated clients come from.
        //
        // A caller does not hand a client an address any more; it hands it the factory, and the
        // factory looks the address up under the name the client asks for. So this is not test
        // scaffolding around the real construction path - it IS the real path, with the ports the
        // fixture happened to get. The name comes from the generated constant, which is what makes
        // "the client asks for X" and "the configuration answers to X" one fact instead of two.
        public static IServiceClientFactory Clients { get; private set; }

        [AssemblyInitialize]
        public static async Task Start(TestContext context)
        {
            var restPort = _FreePort();
            var grpcPort = _FreePort();

            _host = BaseServiceHost.Create<SalesServiceHost>(
                new[]
                {
                    // the application name matters: MVC discovers its controllers through the
                    // dependency context of the assembly it is named after, and under a test run the
                    // entry assembly is the test runner
                    "--applicationName", "Sales.Service",
                    $"--Kestrel:Endpoints:Rest:Url=http://127.0.0.1:{restPort}",
                    "--Kestrel:Endpoints:Rest:Protocols=Http1",
                    $"--Kestrel:Endpoints:Grpc:Url=http://127.0.0.1:{grpcPort}",
                    "--Kestrel:Endpoints:Grpc:Protocols=Http2",
                    // no scrape cache: the observability tests ask "was that order counted", and a
                    // cached answer is one taken before it happened
                    "--Metrics:ScrapeCacheMilliseconds=0",
                },
                SalesServiceHost.DefaultOptions);

            await _host.StartAsync();

            RestAddress = $"http://127.0.0.1:{restPort}";
            GrpcAddress = $"http://127.0.0.1:{grpcPort}";

            Clients = _BuildClientFactory();
        }

        private static IServiceClientFactory _BuildClientFactory()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>()
                {
                    // Two entries because this deployment is cleartext: without TLS there is no
                    // ALPN, so REST and gRPC cannot share a port. With TLS the second one is
                    // unnecessary and the factory falls back to the first.
                    [$"Services:{OrderIF_v1_RestClient.ServiceName}:BaseAddress"] = RestAddress,
                    [$"Services:{OrderIF_v1_GrpcClient.ServiceName}:GrpcAddress"] = GrpcAddress,
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddServiceKitClients();

            return services.BuildServiceProvider().GetRequiredService<IServiceClientFactory>();
        }

        [AssemblyCleanup]
        public static async Task Stop()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }

        // Asking the operating system rather than hard-coding: two test runs on one machine must not
        // collide.
        private static int _FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}

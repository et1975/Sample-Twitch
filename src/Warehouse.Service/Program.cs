namespace Warehouse.Service
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Components.Consumers;
    using Components.StateMachines;
    using MassTransit;
    using MassTransit.Definition;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DependencyCollector;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Events;


    class Program
    {
        static TelemetryClient _telemetryClient;
        static DependencyTrackingTelemetryModule _module;

        static async Task Main(string[] args)
        {
            var isService = !(Debugger.IsAttached || args.Contains("--console"));

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var builder = new HostBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", true);
                    config.AddEnvironmentVariables();

                    if (args != null)
                        config.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    _module = new DependencyTrackingTelemetryModule();
                    _module.IncludeDiagnosticSourceActivities.Add("MassTransit");

                    TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();
                    configuration.InstrumentationKey = "YOUR_AI_KEY";
                    configuration.TelemetryInitializers.Add(new HttpDependenciesParsingTelemetryInitializer());

                    _telemetryClient = new TelemetryClient(configuration);
                    var storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=YOUR_SA;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net");
                    var tableClient = storageAccount.CreateCloudTableClient();

                    _module.Initialize(configuration);

                    services.TryAddSingleton(KebabCaseEndpointNameFormatter.Instance);
                    services.AddMassTransit(cfg =>
                    {
                        cfg.AddConsumersFromNamespaceContaining<AllocateInventoryConsumer>();
                        cfg.AddSagaStateMachine<AllocationStateMachine, AllocationState>(typeof(AllocateStateMachineDefinition))
                            .AzureTableRepository(r =>
                            {
                                r.ConnectionFactory(() => { 
                                    var table = tableClient.GetTableReference("Allocations");
                                    table.CreateIfNotExists();
                                    return table; });
                            });

                        cfg.UsingAzureServiceBus(ConfigureBus);
                    });

                    services.AddHostedService<MassTransitConsoleHostedService>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddSerilog(dispose: true);
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                });

            if (isService)
                await builder.UseWindowsService().Build().RunAsync();
            else
                await builder.RunConsoleAsync();

            _telemetryClient?.Flush();
            _module?.Dispose();

            Log.CloseAndFlush();
        }

        static void ConfigureBus(IBusRegistrationContext busRegistrationContext, MassTransit.Azure.ServiceBus.Core.IServiceBusBusFactoryConfigurator configurator)
        {
            configurator.Host("Endpoint=YOUR_SB_CS");
            configurator.UseServiceBusMessageScheduler();

            configurator.ConfigureEndpoints(busRegistrationContext);
        }
    }
}
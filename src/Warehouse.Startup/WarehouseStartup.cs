namespace Warehouse.Startup
{
    using Components.Consumers;
    using Components.StateMachines;
    using MassTransit;
    using MassTransit.ExtensionsDependencyInjectionIntegration;
    using MassTransit.Platform.Abstractions;
    using Microsoft.Azure.Cosmos.Table;
    using Microsoft.Extensions.DependencyInjection;


    public class WarehouseStartup :
        IPlatformStartup
    {
        public void ConfigureMassTransit(IServiceCollectionBusConfigurator configurator, IServiceCollection services)
        {
            var storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=YOUR_SA;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net");
            var tableClient = storageAccount.CreateCloudTableClient();
            configurator.AddConsumersFromNamespaceContaining<AllocateInventoryConsumer>();
            configurator.AddSagaStateMachine<AllocationStateMachine, AllocationState>(typeof(AllocateStateMachineDefinition))
                .AzureTableRepository(r =>
                {
                    r.ConnectionFactory(() => tableClient.GetTableReference("Allocations"));
                });
        }

        public void ConfigureBus<TEndpointConfigurator>(IBusFactoryConfigurator<TEndpointConfigurator> configurator, IBusRegistrationContext context)
            where TEndpointConfigurator : IReceiveEndpointConfigurator
        {
        }
    }
}
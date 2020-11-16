namespace Sample.Startup
{
    using Components.BatchConsumers;
    using Components.Consumers;
    using Components.CourierActivities;
    using Components.StateMachines;
    using Components.StateMachines.OrderStateMachineActivities;
    using MassTransit;
    using MassTransit.ExtensionsDependencyInjectionIntegration;
    using Microsoft.Azure.Cosmos.Table;
    using MassTransit.Platform.Abstractions;
    using Microsoft.Extensions.DependencyInjection;
    using Warehouse.Contracts;


    public class SampleStartup :
        IPlatformStartup
    {
        public void ConfigureMassTransit(IServiceCollectionBusConfigurator configurator, IServiceCollection services)
        {
            services.AddScoped<AcceptOrderActivity>();

            configurator.AddConsumersFromNamespaceContaining<SubmitOrderConsumer>();
            configurator.AddActivitiesFromNamespaceContaining<AllocateInventoryActivity>();
            configurator.AddConsumersFromNamespaceContaining<RoutingSlipBatchEventConsumer>();
            var storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=YOUR_SA;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net");
            var tableClient = storageAccount.CreateCloudTableClient();

            configurator.AddSagaStateMachine<OrderStateMachine, OrderState>(typeof(OrderStateMachineDefinition))
                .AzureTableRepository(r =>
                {
                    r.ConnectionFactory(() => tableClient.GetTableReference("Orders"));
                });

            configurator.AddRequestClient<AllocateInventory>();
        }

        public void ConfigureBus<TEndpointConfigurator>(IBusFactoryConfigurator<TEndpointConfigurator> configurator,
            IBusRegistrationContext context)
            where TEndpointConfigurator : IReceiveEndpointConfigurator
        {
            // configurator.UseMessageData(new AzureStorageMessageDataRepository("mongodb://mongo", "attachments"));
        }
    }
}
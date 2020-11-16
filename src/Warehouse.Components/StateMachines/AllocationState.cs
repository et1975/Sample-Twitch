namespace Warehouse.Components.StateMachines
{
    using System;
    using Automatonymous;
    using MassTransit.Saga;

    public class AllocationState :
        SagaStateMachineInstance,
        ISagaVersion
    {
        
        public Guid CorrelationId { get; set; }

        public string CurrentState { get; set; }

        public Guid? HoldDurationToken { get; set; }

        public int Version { get; set; }

        public string ETag { get; set; }    }
}
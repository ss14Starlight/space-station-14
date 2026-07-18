using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Pow3r;
using Content.Shared.NodeContainer;
using Robust.Shared.Utility;

namespace Content.Server.Power.NodeGroups;

public abstract class BasePowerNet<TNetType> : BaseNetConnectorNodeGroup<TNetType>, IBasePowerNet
    where TNetType : IBasePowerNet
{
    [ViewVariables] public readonly List<Entity<PowerConsumerComponent>> Consumers = new();
    [ViewVariables] public readonly List<Entity<PowerSupplierComponent>> Suppliers = new();
    public PowerNetSystem PowerNetSystem = default!;

    [ViewVariables]
    public PowerState.Network NetworkNode { get; } = new();

    public override void Initialize(Node sourceNode, IEntityManager entMan)
    {
        base.Initialize(sourceNode, entMan);
        PowerNetSystem = entMan.EntitySysManager.GetEntitySystem<PowerNetSystem>();
    }

    public bool IsConnectedNetwork => NodeCount > 1;

    public void AddConsumer(Entity<PowerConsumerComponent> consumer)
    {
        DebugTools.Assert(consumer.Comp.NetworkLoad.LinkedNetwork == default);
        consumer.Comp.NetworkLoad.LinkedNetwork = default;
        Consumers.Add(consumer);
        QueueNetworkReconnect();
    }

    public void RemoveConsumer(Entity<PowerConsumerComponent> consumer)
    {
        // Linked network can be default if it was re-connected twice in one tick.
        DebugTools.Assert(consumer.Comp.NetworkLoad.LinkedNetwork == default || consumer.Comp.NetworkLoad.LinkedNetwork == NetworkNode.Id);
        consumer.Comp.NetworkLoad.LinkedNetwork = default;
        Consumers.Remove(consumer);
        QueueNetworkReconnect();
    }

    public void AddSupplier(Entity<PowerSupplierComponent> supplier)
    {
        DebugTools.Assert(supplier.Comp.NetworkSupply.LinkedNetwork == default);
        supplier.Comp.NetworkSupply.LinkedNetwork = default;
        Suppliers.Add(supplier);
        QueueNetworkReconnect();
    }

    public void RemoveSupplier(Entity<PowerSupplierComponent> supplier)
    {
        // Linked network can be default if it was re-connected twice in one tick.
        DebugTools.Assert(supplier.Comp.NetworkSupply.LinkedNetwork == default || supplier.Comp.NetworkSupply.LinkedNetwork == NetworkNode.Id);
        supplier.Comp.NetworkSupply.LinkedNetwork = default;
        Suppliers.Remove(supplier);
        QueueNetworkReconnect();
    }

    public abstract void QueueNetworkReconnect();
}

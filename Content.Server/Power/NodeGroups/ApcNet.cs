using System.Linq;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using JetBrains.Annotations;

namespace Content.Server.Power.NodeGroups
{
    public interface IApcNet : IBasePowerNet
    {
        void AddApc(Entity<ApcComponent> apc);

        void RemoveApc(Entity<ApcComponent> apc);

        void AddPowerProvider(Entity<ApcPowerProviderComponent> provider);

        void RemovePowerProvider(Entity<ApcPowerProviderComponent> provider);

        void QueueNetworkReconnect();
    }

    [NodeGroup(NodeGroupID.Apc)]
    [UsedImplicitly]
    public sealed partial class ApcNet : BasePowerNet<IApcNet>, IApcNet
    {
        [ViewVariables] public readonly List<Entity<ApcComponent>> Apcs = new();
        [ViewVariables] public readonly List<Entity<ApcPowerProviderComponent>> Providers = new();

        //Debug property
        [ViewVariables] private int TotalReceivers => Providers.Sum(provider => provider.Comp.LinkedReceivers.Count);

        [ViewVariables]
        private IEnumerable<Entity<ApcPowerReceiverComponent>> AllReceivers =>
            Providers.SelectMany(provider => provider.Comp.LinkedReceivers);

        public override void Initialize(Node sourceNode, IEntityManager entMan)
        {
            base.Initialize(sourceNode, entMan);
            PowerNetSystem.InitApcNet(this);
        }

        public override void AfterRemake(IEnumerable<IGrouping<INodeGroup?, Node>> newGroups)
        {
            base.AfterRemake(newGroups);

            PowerNetSystem?.DestroyApcNet(this);
        }

        public void AddApc(Entity<ApcComponent> apc)
        {
            if (EntMan.TryGetComponent(apc.Owner, out PowerNetworkBatteryComponent? netBattery))
                netBattery.NetworkBattery.LinkedNetworkDischarging = default;

            QueueNetworkReconnect();
            Apcs.Add(apc);
        }

        public void RemoveApc(Entity<ApcComponent> apc)
        {
            if (EntMan.TryGetComponent(apc.Owner, out PowerNetworkBatteryComponent? netBattery))
                netBattery.NetworkBattery.LinkedNetworkDischarging = default;

            QueueNetworkReconnect();
            Apcs.Remove(apc);
        }

        public void AddPowerProvider(Entity<ApcPowerProviderComponent> provider)
        {
            Providers.Add(provider);

            QueueNetworkReconnect();
        }

        public void RemovePowerProvider(Entity<ApcPowerProviderComponent> provider)
        {
            Providers.Remove(provider);

            QueueNetworkReconnect();
        }

        public override void QueueNetworkReconnect()
        {
            PowerNetSystem?.QueueReconnectApcNet(this);
        }

        protected override void SetNetConnectorNet(EntityUid uid, IBaseNetConnectorComponent<IApcNet> netConnectorComponent)
        {
            netConnectorComponent.SetNet(uid, this);
        }

        public override string? GetDebugData()
        {
            // This is just recycling the multi-tool examine.

            var ps = PowerNetSystem.GetNetworkStatistics(NetworkNode);

            float storageRatio = ps.InStorageCurrent / Math.Max(ps.InStorageMax, 1.0f);
            float outStorageRatio = ps.OutStorageCurrent / Math.Max(ps.OutStorageMax, 1.0f);
            return @$"Current Supply: {ps.SupplyCurrent:G3}
From Batteries: {ps.SupplyBatteries:G3}
Theoretical Supply: {ps.SupplyTheoretical:G3}
Ideal Consumption: {ps.Consumption:G3}
Input Storage: {ps.InStorageCurrent:G3} / {ps.InStorageMax:G3} ({storageRatio:P1})
Output Storage: {ps.OutStorageCurrent:G3} / {ps.OutStorageMax:G3} ({outStorageRatio:P1})";
        }
    }
}

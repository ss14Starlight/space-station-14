using Content.Server.Power.Components;
using Content.Server.Power.Pow3r;

namespace Content.Server.Power.NodeGroups
{
    public interface IBasePowerNet
    {
        /// <summary>
        /// Indicates whether this network forms some form of connection (more than one node).
        /// </summary>
        /// <remarks>
        /// Even "unconnected" power devices form a single-node power network all by themselves.
        /// To players, this doesn't look like they're connected to anything.
        /// This property accounts for this and forms a more intuitive check.
        /// </remarks>
        bool IsConnectedNetwork { get; }

        void AddConsumer(Entity<PowerConsumerComponent> consumer);

        void RemoveConsumer(Entity<PowerConsumerComponent> consumer);

        void AddSupplier(Entity<PowerSupplierComponent> supplier);

        void RemoveSupplier(Entity<PowerSupplierComponent> supplier);

        PowerState.Network NetworkNode { get; }
    }
}

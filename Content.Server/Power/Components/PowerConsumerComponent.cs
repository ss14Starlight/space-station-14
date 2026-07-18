using Content.Server.Power.NodeGroups;
using Content.Server.Power.Pow3r;

namespace Content.Server.Power.Components
{
    /// <summary>
    ///     Draws power directly from an MV or HV wire it is on top of.
    /// </summary>
    [RegisterComponent]
    public sealed partial class PowerConsumerComponent : BaseNetConnectorComponent<IBasePowerNet>
    {
        /// <summary>
        ///     How much power this needs to be fully powered.
        /// </summary>
        [DataField("drawRate")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float DrawRate { get => NetworkLoad.DesiredPower; set => NetworkLoad.DesiredPower = value; }

        [DataField("showInMonitor")]
        [ViewVariables(VVAccess.ReadWrite)]
        public bool ShowInMonitor { get; set; } = true;

        /// <summary>
        ///     How much power this is currently receiving from <see cref="PowerSupplierComponent"/>s.
        /// </summary>
        [ViewVariables]
        public float ReceivedPower => NetworkLoad.ReceivingPower;

        public float LastReceived = float.NaN;

        public PowerState.Load NetworkLoad { get; } = new();

        protected override void AddSelfToNet(EntityUid uid, IBasePowerNet powerNet)
        {
            powerNet.AddConsumer((uid, this));
        }

        protected override void RemoveSelfFromNet(EntityUid uid, IBasePowerNet powerNet)
        {
            powerNet.RemoveConsumer((uid, this));
        }
    }
}

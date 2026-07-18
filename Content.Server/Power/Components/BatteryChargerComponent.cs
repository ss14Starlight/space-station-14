using Content.Server.Power.NodeGroups;
using Content.Shared.Power.Components;

namespace Content.Server.Power.Components
{
    /// <summary>
    ///     Connects the loading side of a <see cref="BatteryComponent"/> to a non-APC power network.
    /// </summary>
    [RegisterComponent]
    public sealed partial class BatteryChargerComponent : BasePowerNetComponent
    {
        protected override void AddSelfToNet(EntityUid uid, IPowerNet net)
        {
            net.AddCharger((uid, this));
        }

        protected override void RemoveSelfFromNet(EntityUid uid, IPowerNet net)
        {
            net.RemoveCharger((uid, this));
        }
    }
}

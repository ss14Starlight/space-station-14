using Content.Server.Power.NodeGroups;

namespace Content.Server.Power.Components
{
    [RegisterComponent]
    public sealed partial class BatteryDischargerComponent : BasePowerNetComponent
    {
        protected override void AddSelfToNet(EntityUid uid, IPowerNet net)
        {
            net.AddDischarger((uid, this));
        }

        protected override void RemoveSelfFromNet(EntityUid uid, IPowerNet net)
        {
            net.RemoveDischarger((uid, this));
        }
    }
}

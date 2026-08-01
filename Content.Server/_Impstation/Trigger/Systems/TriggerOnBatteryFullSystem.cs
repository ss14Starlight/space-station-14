using Content.Server._Impstation.Trigger.Components.Triggers;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Trigger.Systems;
using Content.Shared.Power;
using Content.Shared.Power.Components;

namespace Content.Server._Impstation.Trigger.Systems;

public sealed partial class TriggerOnBatteryFullSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private BatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnBatteryFullComponent, ChargeChangedEvent>(OnChargeChanged);
    }

    private void OnChargeChanged(Entity<TriggerOnBatteryFullComponent> ent, ref ChargeChangedEvent args)
    {
        if (TryComp(ent.Owner, out BatteryComponent? battery) && _battery.IsFull((ent.Owner, battery)))
        {
            _trigger.Trigger(ent);
        }
    }
}

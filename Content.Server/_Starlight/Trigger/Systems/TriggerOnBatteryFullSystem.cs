using Content.Server._Starlight.Trigger.Components.Triggers;
using Content.Server.Power.EntitySystems;
using Content.Shared.Trigger.Systems;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._Starlight.Trigger.Systems;

public sealed class TriggerOnBatteryFullSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly PredictedBatterySystem _predictedBattery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnBatteryFullComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<TriggerOnBatteryFullComponent, PredictedBatteryChargeChangedEvent>(OnPredictedChargeChanged);
    }

    private void OnChargeChanged(Entity<TriggerOnBatteryFullComponent> ent, ref ChargeChangedEvent args)
    {
        if (TryComp(ent.Owner, out BatteryComponent? battery) && _battery.IsFull((ent.Owner, battery)))
        {
            _trigger.Trigger(ent);
        }
    }

    private void OnPredictedChargeChanged(Entity<TriggerOnBatteryFullComponent> ent, ref PredictedBatteryChargeChangedEvent args)
    {
        if (TryComp(ent.Owner, out PredictedBatteryComponent? battery) && _predictedBattery.IsFull((ent.Owner, battery)))
        {
            _trigger.Trigger(ent);
        }
    }
}

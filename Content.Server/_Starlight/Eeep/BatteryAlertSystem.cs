using Content.Shared.Alert;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Rounding;

namespace Content.Server._Starlight.Eeep;

/// <summary>
/// Updates battery alerts for entities that have direct PredictedBatteryComponent.
/// Similar to borg/ninja battery systems but for mobs with batteries directly on them.
/// </summary>
public sealed class BatteryAlertSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly PredictedBatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatteryAlertComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BatteryAlertComponent, PredictedBatteryChargeChangedEvent>(OnChargeChanged);
    }

    private void OnStartup(Entity<BatteryAlertComponent> ent, ref ComponentStartup args) =>
        UpdateAlert(ent);

    private void OnChargeChanged(Entity<BatteryAlertComponent> ent, ref PredictedBatteryChargeChangedEvent args) =>
        UpdateAlert(ent);

    private void UpdateAlert(Entity<BatteryAlertComponent> ent)
    {
        if (!TryComp<PredictedBatteryComponent>(ent, out var battery))
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
            return;
        }

        var severity = ContentHelpers.RoundToLevels(
            MathF.Max(0f, _battery.GetCharge((ent.Owner, battery))),
            battery.MaxCharge,
            10); // 0-10 levels like borgs
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, (short)severity);
    }
}

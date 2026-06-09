using Content.Server.Actions;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Actions.EntitySystems;

public sealed class ZoomiesSystem : SharedZoomiesSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly AlertsSystem _alert = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoomiesComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZoomiesComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZoomiesComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<ZoomiesActionEvent>(OnAction);
    }

    private void OnStartup(EntityUid uid, ZoomiesComponent comp, ComponentStartup ev) =>
        _action.AddAction(uid, ref comp.ActionEntity, comp.Action);

    private void OnShutdown(EntityUid uid, ZoomiesComponent comp, ComponentShutdown ev) =>
        _action.RemoveAction(uid, comp.ActionEntity);

    private void OnRefreshMovementSpeed(EntityUid uid, ZoomiesComponent comp, RefreshMovementSpeedModifiersEvent ev)
    {
        if (_timing.CurTime < comp.EffectEndTime && comp.SpeedModifier is not null)
            ev.ModifySpeed(comp.SpeedModifier.Value);
    }

    private void OnAction(ZoomiesActionEvent ev)
    {
        if (ev.Handled) return;
        var uid = ev.Performer;
        if (!TryComp<ZoomiesComponent>(uid, out var comp)) return;

        var duration = comp.Duration ?? TimeSpan.Zero;
        var endTime = _timing.CurTime + duration;
        comp.EffectEndTime = endTime;

        if (comp.SpeedModifier is not null)
            _speed.RefreshMovementSpeedModifiers(uid);

        if(comp.HungerDrain is not null)
            _hunger.AddHungerDrain(uid, comp.HungerDrain.Value, endTime);

        if(comp.ThirstDrain is not null)
            _thirst.AddThirstDrain(uid, comp.ThirstDrain.Value, endTime);

        _alert.ShowAlert(uid, comp.ZoomiesAlert);
        ev.Handled = true;
        comp.Active = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoomiesComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active || _timing.CurTime < comp.EffectEndTime) continue;
            comp.Active = false;
            _speed.RefreshMovementSpeedModifiers(uid);
            _hunger.RemoveHungerDrain(uid, comp.EffectEndTime);
            _thirst.RemoveThirstDrain(uid, comp.EffectEndTime);
            _alert.ClearAlert(uid, comp.ZoomiesAlert);
        }
    }
}

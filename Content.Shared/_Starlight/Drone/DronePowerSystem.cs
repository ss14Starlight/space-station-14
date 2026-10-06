using Content.Shared._Starlight.Drone.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.PowerCell;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Drone;

/// <summary>
/// Power handling for drones, similar to SharedBorgsSystem
/// </summary>
public sealed partial class DronePowerSystem : EntitySystem
{
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    [SubscribeLocalEvent]
    private void OnPowerCellSlotEmpty(Entity<DroneComponent> ent, ref PowerCellSlotEmptyEvent args)
        => SetActive(ent, false);

    [SubscribeLocalEvent]
    private void OnPowerCellChanged(Entity<DroneComponent> ent, ref PowerCellChangedEvent args)
        => TryActivate(ent);

    [SubscribeLocalEvent]
    private static void OnRefreshMovementSpeedModifiers(Entity<DroneComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active)
            return;

        args.ModifySpeed(ent.Comp.UnpoweredSpeedModifier, ent.Comp.UnpoweredSpeedModifier);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        if (curTime < _nextCheck)
            return;

        _nextCheck = curTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<DroneComponent>();
        while (query.MoveNext(out var uid, out var drone))
        {
            TryActivate((uid, drone));
        }
    }

    private bool TryActivate(Entity<DroneComponent> ent)
    {
        if (ent.Comp.Active)
            return true;

        if (!_powerCell.HasDrawCharge(ent.Owner))
            return false;

        SetActive(ent, true);
        return true;
    }

    public void SetActive(Entity<DroneComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        Dirty(ent.Owner, ent.Comp);

        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent.Owner);

        // Access only works with power and a player in control.
        var hasMind = TryComp<MindContainerComponent>(ent, out var mind) && mind.HasMind;
        _access.SetAccessEnabled(ent.Owner, active && hasMind);

        foreach (var action in _actions.GetActions(ent.Owner))
        {
            if (MetaData(action.Owner).EntityPrototype?.ID is { } id && ent.Comp.PowerlessActions.Contains(id))
                continue;

            _actions.SetEnabled(action.AsNullable(), active);
        }

        // Turn the light off
        if (!active && TryComp<HandheldLightComponent>(ent, out var light))
            _handheldLight.TurnOff((ent.Owner, light), makeNoise: false);
    }
}

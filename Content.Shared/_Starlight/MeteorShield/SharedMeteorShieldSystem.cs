using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.MeteorShield;

public abstract partial class SharedMeteorShieldSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedStationSystem _station = default!;

    [SubscribeLocalEvent]
    private void OnSatelliteActivate(Entity<MeteorShieldSatelliteComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        TryToggle((ent, ent.Comp), args.User);
    }

    [SubscribeLocalEvent]
    private void OnSatelliteExamined(Entity<MeteorShieldSatelliteComponent> ent, ref ExaminedEvent args)
        => args.PushMarkup(Loc.GetString("meteor-shield-satellite-examine",
            ("designation", Designation(ent.Comp)),
            ("active", ent.Comp.Active)));

    public static string Designation(MeteorShieldSatelliteComponent comp) => $"MSAT-{comp.Designation}";

    /// <summary>
    /// Switches satellite. It only switches on while floating in open space, and stays locked in place while on.
    /// </summary>
    public bool TryToggle(Entity<MeteorShieldSatelliteComponent?> ent, EntityUid? user)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!ent.Comp.Active && !IsInSpace(ent))
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("meteor-shield-satellite-not-in-space"), ent, user.Value);
            return false;
        }

        SetActive((ent, ent.Comp), !ent.Comp.Active);
        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);
        return true;
    }

    /// <summary>
    /// Turns the satellite on or off. Active satellite is held still as a static body.
    /// </summary>
    protected void SetActive(Entity<MeteorShieldSatelliteComponent> ent, bool active)
    {
        ent.Comp.Active = active;
        ent.Comp.NextCheck = _timing.CurTime;
        Dirty(ent);

        if (TryComp<PhysicsComponent>(ent, out var physics))
        {
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
            _physics.SetAngularVelocity(ent, 0f, body: physics);
            _physics.SetBodyType(ent, active ? BodyType.Static : BodyType.Dynamic, body: physics);
        }

        _appearance.SetData(ent, MeteorShieldSatelliteVisuals.Active, active);
    }

    /// <summary>
    /// True when the satellite floats off any grid, or above an empty tile of a station's main grid.
    /// Every other grid counts as a shuttle or wreck, so satellites never work there, even over a hull gap.
    /// </summary>
    public bool IsInSpace(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return true;

        if (!TryComp<StationMemberComponent>(gridUid, out var member))
            return false;

        if (TryComp<StationDataComponent>(member.Station, out var data)
            && _station.GetLargestGrid((member.Station, data)) != gridUid)
            return false;

        return !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tile) || tile.Tile.IsEmpty;
    }
}

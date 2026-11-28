using System.Linq;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared._Starlight.BluespaceArtillery;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.Pinpointer;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._Starlight.BluespaceArtillery;

public sealed partial class BluespaceArtillerySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private PopupSystem _popup = default!;

    private const int FootprintLeft = 6;
    private const int FootprintRight = 4;
    private const int FootprintHalfHeight = 1;

    private void InitializeConsole()
        => Subs.BuiEvents<BluespaceArtilleryConsoleComponent>(BluespaceArtilleryUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<BluespaceArtilleryBuildMessage>(OnBuild);
            subs.Event<BluespaceArtillerySelectTargetMessage>(OnSelectTarget);
            subs.Event<BluespaceArtilleryChargeMessage>(OnCharge);
            subs.Event<BluespaceArtilleryFireMessage>(OnFire);
        });

    private void OnConsoleOpened(Entity<BluespaceArtilleryConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (ent.Comp.Cannon == null)
            ent.Comp.Cannon = FindNearbyCannon(ent);

        UpdateConsole(ent);
    }

    [SubscribeLocalEvent]
    private void OnConsolePowerChanged(Entity<BluespaceArtilleryConsoleComponent> ent, ref PowerChangedEvent args)
        => UpdateConsole(ent);

    private void OnBuild(Entity<BluespaceArtilleryConsoleComponent> ent, ref BluespaceArtilleryBuildMessage args)
    {
        if (ent.Comp.Cannon is { } existing && Exists(existing))
            return;

        ent.Comp.Notice = TryBuild(ent, out var reason)
            ? Loc.GetString("bsa-console-notice-built")
            : reason;

        if (ent.Comp.Cannon is { } built)
        {
            _adminLog.Add(LogType.Construction, LogImpact.High,
                $"{ToPrettyString(args.Actor):player} assembled the bluespace artillery {ToPrettyString(built):cannon}");
        }

        UpdateConsole(ent);
    }

    private void OnSelectTarget(Entity<BluespaceArtilleryConsoleComponent> ent, ref BluespaceArtillerySelectTargetMessage args)
    {
        if (!TryGetEntity(args.Target, out var target) || !IsValidTarget(target.Value))
            return;

        ent.Comp.Target = target;
        ent.Comp.Notice = null;
        ent.Comp.Operator = args.Actor;

        if (TryGetCannon(ent, out var cannon))
        {
            if (!IsCharged(cannon.Comp))
                cannon.Comp.Charging = true;
            else
                CheckReadyAlert(ent, cannon.Comp);
        }

        UpdateConsole(ent);
    }

    private void OnCharge(Entity<BluespaceArtilleryConsoleComponent> ent, ref BluespaceArtilleryChargeMessage args)
    {
        if (!TryGetCannon(ent, out var cannon))
            return;

        cannon.Comp.Charging = args.Charge && !IsCharged(cannon.Comp);
        if (args.Charge)
            ent.Comp.Operator = args.Actor;
        UpdateConsole(ent);
    }

    private void OnFire(Entity<BluespaceArtilleryConsoleComponent> ent, ref BluespaceArtilleryFireMessage args)
    {
        if (!TryGetCannon(ent, out var cannon))
            return;

        if (ent.Comp.Target is not { } target)
        {
            ent.Comp.Notice = Loc.GetString("bsa-console-notice-no-target");
            UpdateConsole(ent);
            return;
        }

        ent.Comp.Operator = args.Actor;
        if (TryFire((cannon.Owner, cannon.Comp), target, args.Actor, out var reason))
        {
            ent.Comp.Notice = Loc.GetString("bsa-console-notice-fired");
            ent.Comp.AlertedTarget = null;
        }
        else
        {
            ent.Comp.Notice = reason;
        }

        UpdateConsole(ent);
    }

    private bool TryGetCannon(Entity<BluespaceArtilleryConsoleComponent> console, out Entity<BluespaceArtilleryComponent> cannon)
    {
        cannon = default;
        if (console.Comp.Cannon is not { } uid || !TryComp<BluespaceArtilleryComponent>(uid, out var comp))
            return false;

        cannon = (uid, comp);
        return true;
    }

    private EntityUid? FindNearbyCannon(EntityUid console)
    {
        var consoleCoords = _transform.GetMapCoordinates(console);
        var query = EntityQueryEnumerator<BluespaceArtilleryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var cannon, out var xform))
        {
            if (cannon.Console != null && Exists(cannon.Console.Value))
                continue;

            var coords = _transform.GetMapCoordinates(uid, xform);
            if (coords.MapId != consoleCoords.MapId || (coords.Position - consoleCoords.Position).Length() > 15f)
                continue;

            cannon.Console = console;
            return uid;
        }

        return null;
    }

    private bool TryBuild(Entity<BluespaceArtilleryConsoleComponent> console, out string? reason)
    {
        reason = null;

        if (!TryComp<DeviceLinkSinkComponent>(console, out var consoleSink)
            || consoleSink.LinkedSources.FirstOrDefault(IsFusor) is not { Valid: true } fusor)
        {
            reason = Loc.GetString("bsa-console-notice-no-fusor");
            return false;
        }

        EntityUid? generator = null;
        EntityUid? bore = null;
        if (TryComp<DeviceLinkSourceComponent>(fusor, out var fusorSource))
        {
            foreach (var linked in fusorSource.LinkedPorts.Keys)
            {
                if (!TryComp<BluespaceArtilleryPartComponent>(linked, out var part))
                    continue;

                if (part.Part == BluespaceArtilleryPart.Generator)
                    generator = linked;
                else if (part.Part == BluespaceArtilleryPart.Bore)
                    bore = linked;
            }
        }

        if (generator == null || bore == null)
        {
            reason = Loc.GetString("bsa-console-notice-missing-parts");
            return false;
        }

        var fusorXform = Transform(fusor);
        var generatorXform = Transform(generator.Value);
        var boreXform = Transform(bore.Value);

        if (!fusorXform.Anchored || !generatorXform.Anchored || !boreXform.Anchored)
        {
            reason = Loc.GetString("bsa-console-notice-unanchored");
            return false;
        }

        if (fusorXform.GridUid is not { } gridUid
            || generatorXform.GridUid != gridUid
            || boreXform.GridUid != gridUid
            || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            reason = Loc.GetString("bsa-console-notice-misaligned");
            return false;
        }

        var center = _map.TileIndicesFor(gridUid, grid, fusorXform.Coordinates);
        var back = _map.TileIndicesFor(gridUid, grid, generatorXform.Coordinates);
        var front = _map.TileIndicesFor(gridUid, grid, boreXform.Coordinates);

        Direction facing;
        if (back.Y != center.Y || front.Y != center.Y)
        {
            reason = Loc.GetString("bsa-console-notice-misaligned");
            return false;
        }

        if (back.X < center.X && front.X > center.X)
            facing = Direction.East;
        else if (back.X > center.X && front.X < center.X)
            facing = Direction.West;
        else
        {
            reason = Loc.GetString("bsa-console-notice-misaligned");
            return false;
        }

        var parts = new HashSet<EntityUid> { fusor, generator.Value, bore.Value };
        for (var x = center.X - FootprintLeft; x <= center.X + FootprintRight; x++)
        {
            for (var y = center.Y - FootprintHalfHeight; y <= center.Y + FootprintHalfHeight; y++)
            {
                if (!IsTileFree(gridUid, grid, new Vector2i(x, y), parts))
                {
                    reason = Loc.GetString("bsa-console-notice-no-space");
                    return false;
                }
            }
        }

        var coordinates = _map.GridTileToLocal(gridUid, grid, center);
        foreach (var part in parts)
            QueueDel(part);

        var cannon = SpawnAtPosition(facing == Direction.East ? console.Comp.EastPrototype : console.Comp.WestPrototype, coordinates);
        _transform.SetLocalRotation(cannon, Angle.Zero);
        _transform.AnchorEntity(cannon);

        var cannonComp = EnsureComp<BluespaceArtilleryComponent>(cannon);
        cannonComp.Facing = facing;
        cannonComp.Console = console;
        console.Comp.Cannon = cannon;
        return true;
    }

    private bool IsFusor(EntityUid uid)
        => TryComp<BluespaceArtilleryPartComponent>(uid, out var part) && part.Part == BluespaceArtilleryPart.Fusor;

    private bool IsTileFree(EntityUid gridUid, MapGridComponent grid, Vector2i tile, HashSet<EntityUid> ignore)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (ignore.Contains(uid.Value))
                continue;

            if (TryComp<PhysicsComponent>(uid, out var physics) && physics.CanCollide && physics.Hard)
                return false;
        }

        return true;
    }

    private bool IsValidTarget(EntityUid uid)
    {
        if (HasComp<BluespaceArtilleryTestSiteComponent>(uid))
            return true;

        return TryComp<NavMapBeaconComponent>(uid, out var beacon) && beacon.Enabled && !IsProtected(uid);
    }

    private void UpdateConsoleOf(Entity<BluespaceArtilleryComponent> cannon)
    {
        if (TryComp<BluespaceArtilleryConsoleComponent>(cannon.Comp.Console, out var console))
            UpdateConsole((cannon.Comp.Console.Value, console));
    }

    private void UpdateConsole(Entity<BluespaceArtilleryConsoleComponent> ent)
    {
        var hasCannon = TryGetCannon(ent, out var cannon);
        UpdateConsoleVisuals(ent, hasCannon ? cannon.Comp : null);
        if (hasCannon)
            UpdateCannonVisuals(cannon);

        if (!_ui.IsUiOpen(ent.Owner, BluespaceArtilleryUiKey.Key))
            return;

        if (ent.Comp.Target is { } current && !Exists(current))
            ent.Comp.Target = null;

        var state = new BluespaceArtilleryConsoleUiState
        {
            Connected = hasCannon,
            Notice = ent.Comp.Notice,
            Target = GetNetEntity(ent.Comp.Target),
        };

        if (hasCannon)
        {
            state.Charge = cannon.Comp.ChargeEnergy > 0 ? cannon.Comp.Stored / cannon.Comp.ChargeEnergy : 1f;
            state.Charging = cannon.Comp.Charging;
            state.CooldownEnd = cannon.Comp.CooldownEnd;
            state.DrawRate = cannon.Comp.DrawRate;
            state.ReceivedPower = TryComp<PowerConsumerComponent>(cannon, out var consumer) ? consumer.ReceivedPower : 0f;
            FillTargets(state.Targets);
        }

        _ui.SetUiState(ent.Owner, BluespaceArtilleryUiKey.Key, state);
    }

    private void UpdateConsoleVisuals(EntityUid console, BluespaceArtilleryComponent? cannon)
    {
        var visual = BluespaceArtilleryConsoleState.Idle;
        if (TryComp<ApcPowerReceiverComponent>(console, out var receiver) && !receiver.Powered)
            visual = BluespaceArtilleryConsoleState.Unpowered;
        else if (cannon != null && IsCharged(cannon))
            visual = BluespaceArtilleryConsoleState.Ready;
        else if (cannon is { Charging: true })
            visual = BluespaceArtilleryConsoleState.Charging;

        _appearance.SetData(console, BluespaceArtilleryConsoleVisuals.State, visual);
    }

    private void FillTargets(List<BluespaceArtilleryTargetState> targets)
    {
        var sites = EntityQueryEnumerator<BluespaceArtilleryTestSiteComponent>();
        while (sites.MoveNext(out var uid, out _))
        {
            targets.Add(new BluespaceArtilleryTargetState
            {
                Entity = GetNetEntity(uid),
                Name = Name(uid),
                Location = LocationName(uid),
                TestSite = true,
            });
        }

        var beacons = EntityQueryEnumerator<NavMapBeaconComponent>();
        while (beacons.MoveNext(out var uid, out var beacon))
        {
            if (!beacon.Enabled || Transform(uid).MapID == MapId.Nullspace || IsProtected(uid))
                continue;

            targets.Add(new BluespaceArtilleryTargetState
            {
                Entity = GetNetEntity(uid),
                Name = beacon.Text ?? (beacon.DefaultText is { } text ? Loc.GetString(text) : Name(uid)),
                Location = LocationName(uid),
            });
        }

        targets.Sort((a, b) =>
        {
            var location = string.Compare(a.Location, b.Location, StringComparison.CurrentCultureIgnoreCase);
            return location != 0 ? location : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        });
    }

    private string LocationName(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is { } grid)
            return Name(grid);

        return xform.MapUid is { } map ? Name(map) : string.Empty;
    }
}

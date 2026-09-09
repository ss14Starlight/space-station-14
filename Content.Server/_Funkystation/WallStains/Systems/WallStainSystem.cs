using Content.Server.Atmos.Components;
using Content.Server.Forensics;
using Content.Shared._Funkystation.WallStains;
using Content.Shared._Funkystation.WallStains.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Funkystation.WallStains.Systems;

public sealed partial class WallStainSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly ProtoId<TagPrototype> SoapTag = "Soap";

    private static readonly ProtoId<ReagentPrototype> WaterReagent = "Water";
    private static readonly ProtoId<ReagentPrototype> SpaceCleanerReagent = "SpaceCleaner";

    [Dependency] private SharedMapSystem _map = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private SharedSolutionContainerSystem _solution = null!;
    [Dependency] private ForensicsSystem _forensics = null!;
    [Dependency] private SharedDoAfterSystem _doAfter = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private TagSystem _tag = null!;
    [Dependency] private IRobustRandom _random = null!;
    [Dependency] private IPrototypeManager _prototype = null!;
    [Dependency] private SharedPuddleSystem _puddle = null!;
    [Dependency] private SharedAudioSystem _audio = null!;

    private Shared.Chemistry.Reaction.ReactiveReagentEffectEntry _stainCleanEffectEntry = null!;

    private float _evaporationAccumulator;
    private readonly HashSet<EntityUid> _evaporatingStains = [];
    private readonly List<EntityUid> _evaporatingStainsSnapshot = [];

    public override void Initialize()
    {
        base.Initialize();

        _stainCleanEffectEntry = new Shared.Chemistry.Reaction.ReactiveReagentEffectEntry()
        {
            Methods = [ReactionMethod.Touch],
            Reagents = ["SpaceCleaner", "Bleach"],
            Effects = [new CleanWallStainReaction()]
        };

        SubscribeLocalEvent<StainedWallComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<StainedWallComponent, CleanWallStainDoAfterEvent>(OnCleanDoAfter);
        SubscribeLocalEvent<StainedWallComponent, CleanWallStainsEvent>(OnCleanEvent);
        SubscribeLocalEvent<WallStainComponent, MapInitEvent>(OnStainMapInit);
        SubscribeLocalEvent<WallStainComponent, ComponentShutdown>(OnStainShutdown);
        SubscribeLocalEvent<WallStainComponent, SolutionChangedEvent>(OnStainSolutionChanged);
        SubscribeLocalEvent<SpillableComponent, AfterInteractEvent>(OnSpillableAfterInteract);
        SubscribeLocalEvent<SpillableComponent, PourOnWallDoAfterEvent>(OnPourDoAfter);
        SubscribeLocalEvent<SplashOnWallEvent>(OnSplashOnWall);
    }

    private void OnStainMapInit(Entity<WallStainComponent> entity, ref MapInitEvent args)
    {
        if (!_solution.TryGetSolution(entity.Owner, entity.Comp.SolutionName, out _, out var solution))
            return;

        UpdateEvaporationTracking(entity.Owner, solution);
        if (solution.Volume > FixedPoint2.Zero)
            UpdateVisuals(entity.Owner, entity.Comp, solution);
    }

    private void OnStainShutdown(Entity<WallStainComponent> entity, ref ComponentShutdown args)
    {
        _evaporatingStains.Remove(entity.Owner);
    }

    private void OnStainSolutionChanged(Entity<WallStainComponent> entity, ref SolutionChangedEvent args)
    {
        if (args.Solution.Comp.Id != entity.Comp.SolutionName)
            return;

        var solution = args.Solution.Comp.Solution;
        UpdateEvaporationTracking(entity.Owner, solution);
        if (solution.Volume > FixedPoint2.Zero)
            UpdateVisuals(entity.Owner, entity.Comp, solution);
    }

    private void UpdateEvaporationTracking(EntityUid uid, Solution solution)
    {
        if (solution.Volume <= FixedPoint2.Zero ||
            solution.GetTotalPrototypeQuantity(WaterReagent) > FixedPoint2.Zero ||
            solution.GetTotalPrototypeQuantity(SpaceCleanerReagent) > FixedPoint2.Zero)
        {
            _evaporatingStains.Add(uid);
            return;
        }

        _evaporatingStains.Remove(uid);
    }

    private void OnSplashOnWall(ref SplashOnWallEvent args)
    {
        TrySplashOnWalls(args.Coordinates, args.Solution);
    }

    private void TrySplashOnWalls(EntityCoordinates coords, Solution solution)
    {
        if (solution.Volume <= 0)
            return;

        var gridUid = _transform.GetGrid(coords);
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tilePos = _map.TileIndicesFor(gridUid.Value, grid, coords);
        var checkOffsets = new[]
        {
            new Vector2i(0, 0),
            new Vector2i(0, 1),
            new Vector2i(0, -1),
            new Vector2i(1, 0),
            new Vector2i(-1, 0)
        };

        foreach (var offset in checkOffsets)
        {
            var targetTile = tilePos + offset;
            var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid.Value, grid, targetTile);
            while (anchored.MoveNext(out var ent))
            {
                if (!IsWall(ent.Value))
                    continue;

                ApplyStainToWall(ent.Value, solution, -offset, fraction: 0.25f);
            }
        }
    }

    private bool IsWall(EntityUid uid)
    {
        return HasComp<AirtightComponent>(uid) || _tag.HasTag(uid, WallTag) || _tag.HasTag(uid, WindowTag);
    }

    private FixedPoint2 ApplyStainToWall(EntityUid wallUid, Solution solution, Vector2i direction, float fraction = 1.0f)
    {
        EnsureComp<StainedWallComponent>(wallUid);

        var reactive = EnsureComp<ReactiveComponent>(wallUid);
        reactive.Reactions ??= new();
        if (!reactive.Reactions.Contains(_stainCleanEffectEntry))
            reactive.Reactions.Add(_stainCleanEffectEntry);

        var stainUid = EntityUid.Invalid;
        WallStainComponent? stainComp = null;

        var children = Transform(wallUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp<WallStainComponent>(child, out var existingStain) || existingStain.Direction != direction)
                continue;
            stainUid = child;
            stainComp = existingStain;
            break;
        }

        if (stainUid == EntityUid.Invalid)
        {
            stainUid = Spawn("WallStain", Transform(wallUid).Coordinates);
            _transform.SetParent(stainUid, wallUid);

            var baseOffset = new System.Numerics.Vector2(direction.X * 0.48f, direction.Y * 0.48f);
            if (direction.X != 0)
                baseOffset.Y += _random.NextFloat(-0.35f, 0.35f);
            if (direction.Y != 0)
                baseOffset.X += _random.NextFloat(-0.35f, 0.35f);
            if (direction == Vector2i.Zero)
                baseOffset = new System.Numerics.Vector2(_random.NextFloat(-0.4f, 0.4f), _random.NextFloat(-0.4f, 0.4f));

            _transform.SetLocalPosition(stainUid, baseOffset);
            _transform.SetLocalRotation(stainUid, direction != Vector2i.Zero ? Angle.Zero : _random.NextAngle());

            stainComp = Comp<WallStainComponent>(stainUid);
            stainComp.Direction = direction;
            Dirty(stainUid, stainComp);
        }

        var actualTransfer = FixedPoint2.Zero;

        if (stainComp != null && _solution.TryGetSolution(stainUid, stainComp.SolutionName, out var stainSolution))
        {
            actualTransfer = FixedPoint2.Min(solution.Volume * fraction, stainComp.MaxStainVolume - stainSolution.Value.Comp.Solution.Volume);

            if (actualTransfer > 0)
            {
                var split = solution.Clone().SplitSolution(actualTransfer);
                _solution.TryAddSolution(stainSolution.Value, split);

                var wallForensics = EnsureComp<ForensicsComponent>(wallUid);
                var dnas = _forensics.GetSolutionsDNA(split);
                wallForensics.DNAs.UnionWith(dnas);
            }
        }

        return actualTransfer;
    }

    private void OnSpillableAfterInteract(EntityUid uid, SpillableComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        if (!IsWall(args.Target.Value))
            return;

        if (!_solution.TryGetSolution(uid, component.SolutionName, out var solComp) || solComp.Value.Comp.Solution.Volume <= 0)
            return;

        var solution = solComp.Value.Comp.Solution;
        if (solution.GetTotalPrototypeQuantity(WaterReagent) == solution.Volume)
        {
            _popup.PopupEntity(Loc.GetString("wall-stain-pour-water-blocked"), args.Target.Value, args.User);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("wall-stain-pour-start", ("container", uid)), args.Target.Value, args.User);

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5), new PourOnWallDoAfterEvent(), uid, target: args.Target.Value, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnPourDoAfter(EntityUid uid, SpillableComponent component, PourOnWallDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        args.Handled = true;

        if (!_solution.TryGetSolution(uid, component.SolutionName, out var solComp) || solComp.Value.Comp.Solution.Volume <= 0)
            return;

        var wallUid = args.Target.Value;

        var wallPos = _transform.GetGridTilePositionOrDefault(wallUid);
        var userPos = _transform.GetGridTilePositionOrDefault(args.User);
        var direction = userPos - wallPos;
        direction.X = Math.Clamp(direction.X, -1, 1);
        direction.Y = Math.Clamp(direction.Y, -1, 1);

        var maxPour = FixedPoint2.Min(FixedPoint2.New(15), solComp.Value.Comp.Solution.Volume);
        var pourSolution = solComp.Value.Comp.Solution.Clone().SplitSolution(maxPour);

        var transferred = ApplyStainToWall(wallUid, pourSolution, direction, fraction: 1.0f);

        if (transferred > 0)
        {
            _solution.SplitSolution(solComp.Value, transferred);

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg"), wallUid);
            _popup.PopupEntity(Loc.GetString("wall-stain-pour-success", ("container", uid)), wallUid, args.User);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("wall-stain-pour-full"), wallUid, args.User);
        }
    }

    private void OnInteractUsing(EntityUid uid, StainedWallComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var tool = args.Used;
        var user = args.User;

        if (!IsCleaningTool(tool))
            return;
        if (TryComp<AbsorbentComponent>(tool, out var absorbent))
        {
            if (_solution.TryGetSolution(tool, absorbent.SolutionName, out _, out var solution))
            {
                var absorbentReagents = _puddle.GetAbsorbentReagents(solution);
                if (solution.GetTotalPrototypeQuantity(absorbentReagents) <= 0)
                {
                    _popup.PopupEntity(Loc.GetString("wall-stain-cleaning-dry-rag"), tool, user);
                    args.Handled = true;
                    return;
                }
            }
        }

        _popup.PopupEntity(Loc.GetString("wall-stain-cleaning-start"), uid, user);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5), new CleanWallStainDoAfterEvent(), uid, target: uid, used: tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            args.Handled = true;
        }
    }

    private void OnCleanDoAfter(EntityUid uid, StainedWallComponent component, CleanWallStainDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("wall-stain-cleaning-success"), uid, args.User);

        RaiseLocalEvent(uid, new CleanWallStainsEvent(transformToWater: false));
    }

    private void OnCleanEvent(EntityUid uid, StainedWallComponent component, CleanWallStainsEvent args)
    {
        if (args.TransformToWater)
        {
            var children = Transform(uid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (!TryComp<WallStainComponent>(child, out var stain) ||
                    !_solution.TryGetSolution(child, stain.SolutionName, out var solComp))
                    continue;
                var totalVolume = solComp.Value.Comp.Solution.Volume;
                if (totalVolume <= 0)
                    continue;

                solComp.Value.Comp.Solution.RemoveAllSolution();
                solComp.Value.Comp.Solution.AddReagent(WaterReagent, totalVolume);
                _solution.UpdateChemicals(solComp.Value);
            }

            if (TryComp<ForensicsComponent>(uid, out var forensics))
            {
                forensics.DNAs.Clear();
            }
        }
        else
        {
            var children = Transform(uid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (HasComp<WallStainComponent>(child))
                    QueueDel(child);
            }

            if (TryComp<ForensicsComponent>(uid, out var forensics))
            {
                forensics.DNAs.Clear();
            }

            RemCompDeferred<StainedWallComponent>(uid);
        }
    }

    private bool IsCleaningTool(EntityUid uid)
    {
        return HasComp<AbsorbentComponent>(uid) || _tag.HasTag(uid, SoapTag);
    }

    private void UpdateVisuals(EntityUid uid, WallStainComponent? comp = null, Solution? solution = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (solution == null && !_solution.TryGetSolution(uid, comp.SolutionName, out _, out solution))
            return;

        var color = solution.GetColor(_prototype);
        var stainColor = color.WithAlpha(color.A * 0.6f);
        var stainState = solution.ContainsPrototype(WaterReagent) || solution.ContainsPrototype(SpaceCleanerReagent)
            ? "drip"
            : "splatter";
        if (comp.Color == stainColor && comp.StainState == stainState)
            return;

        comp.Color = stainColor;
        comp.StainState = stainState;
        Dirty(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _evaporationAccumulator += frameTime;
        if (_evaporationAccumulator < 1f)
            return;

        _evaporationAccumulator -= 1f;

        _evaporatingStainsSnapshot.Clear();
        _evaporatingStainsSnapshot.AddRange(_evaporatingStains);

        foreach (var uid in _evaporatingStainsSnapshot)
        {
            if (!TryComp<WallStainComponent>(uid, out var stain))
            {
                _evaporatingStains.Remove(uid);
                continue;
            }

            if (!_solution.TryGetSolution(uid, stain.SolutionName, out var solComp))
            {
                _evaporatingStains.Remove(uid);
                continue;
            }

            var solution = solComp.Value.Comp.Solution;
            if (solution.Volume <= 0)
            {
                _evaporatingStains.Remove(uid);
                QueueDel(uid);
                continue;
            }

            var waterQty = solution.GetTotalPrototypeQuantity(WaterReagent);
            var cleanerQty = solution.GetTotalPrototypeQuantity(SpaceCleanerReagent);

            if (waterQty <= 0 && cleanerQty <= 0)
            {
                _evaporatingStains.Remove(uid);
                continue;
            }

            var evaporationAmount = FixedPoint2.New(0.5f);

            if (waterQty > 0)
            {
                var toRemove = FixedPoint2.Min(evaporationAmount, waterQty);
                solution.RemoveReagent(WaterReagent, toRemove);
                evaporationAmount -= toRemove;
            }

            if (evaporationAmount > 0 && cleanerQty > 0)
            {
                var toRemove = FixedPoint2.Min(evaporationAmount, cleanerQty);
                solution.RemoveReagent(SpaceCleanerReagent, toRemove);
            }

            _solution.UpdateChemicals(solComp.Value);

            if (solution.Volume > 0)
                continue;

            _evaporatingStains.Remove(uid);
            var parent = Transform(uid).ParentUid;

            Spawn("WallStainSparkle", Transform(uid).Coordinates);

            QueueDel(uid);

            if (!parent.IsValid())
                continue;
            var hasOtherStains = false;
            var children = Transform(parent).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (child == uid || !HasComp<WallStainComponent>(child))
                    continue;
                hasOtherStains = true;
                break;
            }

            if (hasOtherStains)
                continue;
            RemCompDeferred<StainedWallComponent>(parent);
            if (TryComp<ReactiveComponent>(parent, out var reactive))
            {
                reactive.Reactions?.Remove(_stainCleanEffectEntry);
            }
        }
    }
}

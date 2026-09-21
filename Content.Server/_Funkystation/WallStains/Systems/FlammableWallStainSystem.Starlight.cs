using Content.Server._Funkystation.ReagentFires.Components;
using Content.Shared._Funkystation.WallStains.Components;
using Content.Shared._Funkystation.ReagentFires;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.WallStains.Systems;

public sealed partial class FlammableWallStainSystem : EntitySystem
{
    [Dependency] private EntityQuery<ReagentPuddleFireComponent> _puddleFireQuery;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery;
    private static readonly ProtoId<DamageTypePrototype> _structuralDamage = "Structural";
    private static readonly ProtoId<DamageTypePrototype> _heatDamage = "Heat";
    private readonly HashSet<EntityUid> _burningStains = [];
    private readonly List<EntityUid> _burningSnapshot = [];
    private const float UpdateInterval = 0.5f;
    private float _updateAccumulator;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<FlammableWallStainComponent> ent, ref MapInitEvent args)
        => RefreshFireState(ent);

    [SubscribeLocalEvent]
    private void OnStartup(Entity<FlammableWallStainComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.OnFire)
            _burningStains.Add(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnSolutionChanged(Entity<FlammableWallStainComponent> ent, ref SolutionChangedEvent args)
    {
        if (_stainQuery.TryComp(ent.Owner, out var stain) && args.Solution.Comp.Id == stain.SolutionName)
            RefreshFireState(ent, stain, args.Solution.Comp.Solution);
    }

    private void UpdateFireVisuals(Entity<FlammableWallStainComponent> ent)
    {
        ent.Comp.FireState = ent.Comp.Flammability > 10 ? 6 : ent.Comp.Flammability > 5 ? 5 : 4;
        var fireColor = GetFireColor(ent.Comp.Flammability);

        var light = EnsureComp<PointLightComponent>(ent.Owner);
        _light.SetEnabled(ent.Owner, true, light);
        _light.SetRadius(ent.Owner, MathF.Max(1.5f, ent.Comp.FireState - 2f), light);
        _light.SetColor(ent.Owner, fireColor, light);
        _light.SetEnergy(ent.Owner, 1.5f, light);

        var wantedSoundPath = ent.Comp.Flammability >= 4
            ? "/Audio/_Funkystation/Effects/Fire/hissing.ogg"
            : "/Audio/_Funkystation/Effects/Fire/bigfire.ogg";

        if (ent.Comp.CurrentPlayingSound != wantedSoundPath)
        {
            if (ent.Comp.PlayingStream != null)
                _audio.Stop(ent.Comp.PlayingStream);

            ent.Comp.PlayingStream = _audio.PlayPvs(new SoundPathSpecifier(wantedSoundPath), ent.Owner,
                AudioParams.Default.WithLoop(true).WithVolume(-8f))?.Entity;
            ent.Comp.CurrentPlayingSound = wantedSoundPath;
        }

        if (ent.Comp.FireEffectEntity == null)
        {
            var parentWall = Transform(ent.Owner).ParentUid;
            if (parentWall.IsValid())
            {
                var fireEnt = Spawn("WallStainFireEffect", Transform(parentWall).Coordinates);
                _transform.SetParent(fireEnt, parentWall);
                _transform.SetLocalPosition(fireEnt, System.Numerics.Vector2.Zero);
                ent.Comp.FireEffectEntity = fireEnt;
            }
        }

        if (ent.Comp.FireEffectEntity is { } fireEntEffect)
        {
            _appearance.SetData(fireEntEffect, ReagentPuddleFireVisuals.FireState, ent.Comp.FireState);
            _appearance.SetData(fireEntEffect, ReagentPuddleFireVisuals.FireColor, fireColor);
        }
    }

    private bool RefreshFireState(Entity<FlammableWallStainComponent> ent,
        WallStainComponent? stain = null,
        Solution? solution = null)
    {
        if (stain == null && !_stainQuery.TryComp(ent.Owner, out stain))
        {
            ent.Comp.Flammability = 0;
            ent.Comp.SelfOxidizing = false;
            if (ent.Comp.OnFire)
                Extinguish(ent);
            return false;
        }

        if (solution == null
            && !_solution.TryGetSolution(ent.Owner, stain.SolutionName, out _, out solution))
        {
            ent.Comp.Flammability = 0;
            ent.Comp.SelfOxidizing = false;
            if (ent.Comp.OnFire)
                Extinguish(ent);
            return false;
        }

        var oldFlammability = ent.Comp.Flammability;
        var flammability = solution.GetSolutionFlammability(_proto);
        ent.Comp.Flammability = flammability;
        ent.Comp.SelfOxidizing = solution.IsSolutionSelfOxidizing(_proto);

        if (flammability <= 0)
        {
            if (ent.Comp.OnFire)
                Extinguish(ent);
            return false;
        }

        if (ent.Comp.OnFire)
        {
            if (flammability != oldFlammability)
                UpdateFireVisuals(ent);
        }
        else
        {
            TryIgniteFromNearbyFire(ent, stain);
        }

        return true;
    }

    /// <summary>
    /// Handles a flammable stain appearing after a neighboring fire has already propagated.
    /// </summary>
    private void TryIgniteFromNearbyFire(Entity<FlammableWallStainComponent> ent, WallStainComponent stain)
    {
        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var wallPos = _transform.GetGridTilePositionOrDefault((ent.Owner, xform));
        var atmosTilePos = wallPos + stain.Direction;

        var puddles = _map.GetAnchoredEntities(gridUid, grid, atmosTilePos);
        while (puddles.MoveNext(out var puddle))
        {
            if (_puddleFireQuery.TryComp(puddle, out var puddleFire) && puddleFire.OnFire)
            {
                Ignite(ent.Owner, ent.Comp);
                return;
            }
        }

        foreach (var offset in _tileAndCardinalOffsets)
        {
            var walls = _map.GetAnchoredEntities(gridUid, grid, wallPos + offset);
            while (walls.MoveNext(out var wall))
            {
                if (!_stainedWallQuery.HasComp(wall))
                    continue;

                var children = Transform(wall.Value).ChildEnumerator;
                while (children.MoveNext(out var child))
                {
                    if (child != ent.Owner
                        && _fireQuery.TryComp(child, out var fire)
                        && fire.OnFire)
                    {
                        Ignite(ent.Owner, ent.Comp);
                        return;
                    }
                }
            }
        }
    }

    private void SpreadFire(EntityUid uid,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i wallPos,
        Vector2i atmosTilePos,
        GasMixture? tileMix,
        int flammability)
    {
        // Puddles are anchored, no spatial lookup is needed.
        var fireEvent = new TileFireEvent(tileMix?.Temperature ?? 600f, 50f * flammability);
        var puddles = _map.GetAnchoredEntities(gridUid, grid, atmosTilePos);
        while (puddles.MoveNext(out var ent))
        {
            if (_puddleQuery.HasComp(ent))
                RaiseLocalEvent(ent.Value, ref fireEvent);
        }

        _toIgnite.Clear();

        foreach (var offset in _tileAndCardinalOffsets)
        {
            var checkWallTile = wallPos + offset;
            var enumerator = _map.GetAnchoredEntities(gridUid, grid, checkWallTile);
            while (enumerator.MoveNext(out var ent))
            {
                if (!_stainedWallQuery.HasComp(ent))
                    continue;

                var children = Transform(ent.Value).ChildEnumerator;
                while (children.MoveNext(out var child))
                {
                    if (child == uid)
                        continue;

                    if (_fireQuery.TryComp(child, out var adjacentFire)
                        && !adjacentFire.OnFire
                        && adjacentFire.Flammability > 0)
                    {
                        _toIgnite.Add((child, adjacentFire));
                    }
                }
            }
        }

        foreach (var (stainUid, fireComp) in _toIgnite)
            Ignite(stainUid, fireComp);
    }
}

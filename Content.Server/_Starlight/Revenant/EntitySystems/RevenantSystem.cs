using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Revenant;
using Content.Shared.Atmos;
using Content.Shared.Mobs.Components;
using Content.Shared.Revenant.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Server.Revenant.EntitySystems;

// Starlight: all revenant abilities added by Starlight live here so the upstream
// RevenantSystem files only carry a single call into InitializeStarlightAbilities().
public sealed partial class RevenantSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedWieldableSystem _wieldable = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<TagPrototype> MisfireBypassUserTag = "BypassUserTagChecks";

    private void InitializeStarlightAbilities()
    {
        SubscribeLocalEvent<RevenantComponent, RevenantChillActionEvent>(OnChillAction);
        SubscribeLocalEvent<RevenantComponent, RevenantMisfireActionEvent>(OnMisfireAction);
    }

    ///<summary>
    /// Activates guns and has them shoot the nearest person
    ///</summary>
    private void OnMisfireAction(EntityUid uid, RevenantComponent component, RevenantMisfireActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<GunComponent>(args.Target, out var gunComp))
            return;

        // Don't fire if the gun is still on its shot cooldown
        // Used before TryUseAbility so it doesn't fail to fire costing the revenant essence.
        if (gunComp.NextFire > _timing.CurTime)
            return;

        if (!TryUseAbility(uid, component, component.misfireCost, component.MisfireDebuffs))
            return;

        args.Handled = true;

        Entity<GunComponent> gun = (args.Target, gunComp);
        var mobStateQuery = GetEntityQuery<MobStateComponent>();
        var gunPos = _transformSystem.GetWorldPosition(args.Target);

        // Find the nearest living mob to shoot.
        var target = _lookup.GetEntitiesInRange(args.Target, component.MisfireTargetRadius)
            .Where(e => mobStateQuery.HasComponent(e) && _mobState.IsAlive(e) &&
                        _interact.InRangeUnobstructed(e, args.Target, -1))
            .OrderBy(e => (_transformSystem.GetWorldPosition(e) - gunPos).LengthSquared())
            .FirstOrDefault();

        if (target == default)
            return;

        //Allows guns that have to be wielded to be fired
        if (TryComp<WieldableComponent>(args.Target, out var wieldable))
            _wieldable.ForceWielded((args.Target, wieldable), true);

        // Bolts unbolted guns and chamber a round so the gun actually fires
        _gun.ForceChamber(args.Target);

        // Turns the gun to face the target so burst fire weapons don't fire their other shots wrongly
        var direction = _transformSystem.GetWorldPosition(target) - gunPos;
        if (direction != Vector2.Zero)
            _transformSystem.SetWorldRotation(args.Target, new Angle(direction) - new Angle(gunComp.DefaultDirection));

        // Certain guns require a user to be able to fire
        _tag.AddTag(args.Target, MisfireBypassUserTag);
        _gun.AttemptShoot(args.Target, gun, Transform(target).Coordinates, target);
        _tag.RemoveTag(args.Target, MisfireBypassUserTag);

        // Cycles guns after shooting so you can shoot again
        _gun.ForceCycle(args.Target);

        // Clear the forced wield so guns are not left in a weird state
        if (wieldable != null)
            _wieldable.ForceWielded((args.Target, wieldable), false);
    }

    ///<summary>
    /// Creates ice tiles and adds freezon per ice tile
    ///</summary>
    private void OnChillAction(EntityUid uid, RevenantComponent component, RevenantChillActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, component, component.chillCost, component.ChillDebuffs))
            return;

        args.Handled = true;

        var xform = Transform(uid);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var map))
            return;

        //The tiles that always spawn
        var coreTiles = _mapSystem.GetTilesIntersecting(
            xform.GridUid.Value,
            map,
            Box2.CenteredAround(_transformSystem.GetWorldPosition(xform),
            new Vector2(component.ChillCoreRadius, component.ChillCoreRadius)))
            .ToArray();

        //The tiles with a random chance of spawning
        var falloffTiles = _mapSystem.GetTilesIntersecting(
            xform.GridUid.Value,
            map,
            Box2.CenteredAround(_transformSystem.GetWorldPosition(xform),
            new Vector2(component.ChillFalloffRadius, component.ChillFalloffRadius)))
            .ToArray();

        //Generate the ice tiles and add the moles for freezon
        foreach (var tileref in falloffTiles)
        {
            //Generate the tiles in a radius that always spawn.
            if(coreTiles.Contains(tileref))
            {
                Spawn("IceCrust", _mapSystem.ToCenterCoordinates(tileref, map));
                _atmosphere.GetTileMixture(xform.GridUid.Value, null, tileref.GridIndices, true)?.AdjustMoles(Gas.Frezon, component.ChillFrezonPerTile);
                continue;
            }

            //Percentage chance to generate ice tiles in the falloff area
            if(_random.Prob(component.ChillFalloffChance)) {
                Spawn("IceCrust", _mapSystem.ToCenterCoordinates(tileref, map));
                _atmosphere.GetTileMixture(xform.GridUid.Value, null, tileref.GridIndices, true)?.AdjustMoles(Gas.Frezon, component.ChillFrezonPerTile);
            }
        }

        return;
    }
}

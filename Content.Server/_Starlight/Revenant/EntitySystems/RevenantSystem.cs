using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Starlight.Revenant;
using Content.Shared.Atmos;
using Content.Shared.Item;
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
    private void OnMisfireAction(Entity<RevenantComponent> ent, ref RevenantMisfireActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<GunComponent>(args.Target, out var gunComp))
            return;

        // Only handheld items can be misfired. Stops revenants from firing ship weapons and anchored weapons.
        if (!HasComp<ItemComponent>(args.Target))
            return;

        // Don't fire if the gun is still on its shot cooldown
        // Used before TryUseAbility so it doesn't fail to fire costing the revenant essence.
        if (gunComp.NextFire > _timing.CurTime)
            return;

        if (!TryUseAbility(ent, ent.Comp, ent.Comp.misfireCost, ent.Comp.MisfireDebuffs))
            return;

        args.Handled = true;

        var gunUid = args.Target;

        Entity<GunComponent> gun = (gunUid, gunComp);
        var mobStateQuery = GetEntityQuery<MobStateComponent>();
        var gunPos = _transformSystem.GetWorldPosition(gunUid);

        // Find the nearest living mob to shoot.
        var target = _lookup.GetEntitiesInRange(gunUid, ent.Comp.MisfireTargetRadius)
            .Where(e => mobStateQuery.HasComponent(e) && _mobState.IsAlive(e) &&
                        _interact.InRangeUnobstructed(e, gunUid, -1))
            .OrderBy(e => (_transformSystem.GetWorldPosition(e) - gunPos).LengthSquared())
            .FirstOrDefault();

        if (target == default)
            return;

        //Allows guns that have to be wielded to be fired
        if (TryComp<WieldableComponent>(gunUid, out var wieldable))
            _wieldable.ForceWielded((gunUid, wieldable), true);

        // Bolts unbolted guns and chamber a round so the gun actually fires
        _gun.ForceChamber(gun.AsNullable());

        // Turns the gun to face the target so burst fire weapons don't fire their other shots wrongly
        var direction = _transformSystem.GetWorldPosition(target) - gunPos;
        if (direction != Vector2.Zero)
            _transformSystem.SetWorldRotation(gunUid, new Angle(direction) - new Angle(gunComp.DefaultDirection));

        // Certain guns require a user to be able to fire
        _tag.AddTag(gunUid, MisfireBypassUserTag);
        _gun.AttemptShoot(gunUid, gun, Transform(target).Coordinates, target);
        _tag.RemoveTag(gunUid, MisfireBypassUserTag);

        // Cycles guns after shooting so you can shoot again
        _gun.ForceCycle(gun.AsNullable());

        // Clear the forced wield so guns are not left in a weird state
        if (wieldable != null)
            _wieldable.ForceWielded((gunUid, wieldable), false);
    }

    ///<summary>
    /// Creates ice tiles and adds freezon per ice tile
    ///</summary>
    private void OnChillAction(Entity<RevenantComponent> ent, ref RevenantChillActionEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(ent);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var map))
            return;

        if (!TryUseAbility(ent, ent.Comp, ent.Comp.chillCost, ent.Comp.ChillDebuffs))
            return;

        args.Handled = true;

        //The tiles that always spawn
        var coreTiles = _mapSystem.GetTilesIntersecting(
            xform.GridUid.Value,
            map,
            Box2.CenteredAround(_transformSystem.GetWorldPosition(xform),
            new Vector2(ent.Comp.ChillCoreRadius, ent.Comp.ChillCoreRadius)))
            .ToArray();

        //The tiles with a random chance of spawning
        var falloffTiles = _mapSystem.GetTilesIntersecting(
            xform.GridUid.Value,
            map,
            Box2.CenteredAround(_transformSystem.GetWorldPosition(xform),
            new Vector2(ent.Comp.ChillFalloffRadius, ent.Comp.ChillFalloffRadius)))
            .ToArray();

        //Generate the ice tiles and add the moles for freezon
        foreach (var tileref in falloffTiles)
        {
            //Generate the tiles in a radius that always spawn.
            if(coreTiles.Contains(tileref))
            {
                Spawn("IceCrust", _mapSystem.ToCenterCoordinates(tileref, map));
                _atmosphere.GetTileMixture(xform.GridUid.Value, null, tileref.GridIndices, true)?.AdjustMoles(Gas.Frezon, ent.Comp.ChillFrezonPerTile);
                continue;
            }

            //Percentage chance to generate ice tiles in the falloff area
            if(_random.Prob(ent.Comp.ChillFalloffChance)) {
                Spawn("IceCrust", _mapSystem.ToCenterCoordinates(tileref, map));
                _atmosphere.GetTileMixture(xform.GridUid.Value, null, tileref.GridIndices, true)?.AdjustMoles(Gas.Frezon, ent.Comp.ChillFrezonPerTile);
            }
        }

        return;
    }
}

using System.Numerics;
using Content.Shared._Starlight.Combat.Effects.Components;
using Content.Shared.Armor;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Materials;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Components;

namespace Content.Shared._Starlight.Combat.Effects.EntitySystems;

/// <summary>
/// Handles spawning spark visual effects when armor with high pierce resistance
/// or Rock material is hit by SP or HP hitscan bullets.
/// </summary>
public abstract partial class SharedArmorSparkEffectSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorSparkEffectComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnArmorDamageModify);
        SubscribeLocalEvent<DamageableComponent, DamageModifyEvent>(OnDamageableDamageModify);
    }

    private bool AlwaysSparks(EntityUid uid, ArmorSparkEffectComponent component)
    {
        if (component.AlwaysSparks)
            return true;

        if (TryComp<PhysicalCompositionComponent>(uid, out var composition) &&
            composition.MaterialComposition.ContainsKey("Rock"))
            return true;

        return false;
    }

    private void OnDamageableDamageModify(EntityUid uid, DamageableComponent component, DamageModifyEvent args)
    {
        if (!TryComp<ArmorSparkEffectComponent>(uid, out var spark))
            return;

        HandleSparkHit(uid, spark, args);
    }

    private void OnArmorDamageModify(EntityUid uid, ArmorSparkEffectComponent component, InventoryRelayedEvent<DamageModifyEvent> args) =>
        HandleSparkHit(uid, component, args.Args);

    private bool HasHighPiercingResistance(EntityUid uid, bool wornArmor)
    {
        var query = new CoefficientQueryEvent(SlotFlags.OUTERCLOTHING);

        if (wornArmor)
        {
            var relayedEvent = new InventoryRelayedEvent<CoefficientQueryEvent>(
                query,
                uid);

            RaiseLocalEvent(uid, relayedEvent);
        }
        else
        {
            RaiseLocalEvent(uid, query);
        }

        var piercingCoefficient =
            query.DamageModifiers.Coefficients.TryGetValue("Piercing", out var coefficient)
                ? coefficient
                : 1f;

        return piercingCoefficient <= 0.2f;
    }

    private void HandleSparkHit(EntityUid uid, ArmorSparkEffectComponent component, DamageModifyEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!IsHitscanDamage(args))
            return;

        // AlwaysSpark and Rock bypass all armor checks.
        if (AlwaysSparks(uid, component))
        {
            var useParent = TryComp<ArmorComponent>(uid, out _);
            SpawnSparkEffect(uid, component, useParent);
            return;
        }

        if (!IsSPOrHPBullet(args))
            return;

        // Worn armor.
        if (TryComp<ArmorComponent>(uid, out _))
        {
            if (!HasHighPiercingResistance(uid, true))
                return;

            SpawnSparkEffect(uid, component, true);
            return;
        }

        // Innate armor.
        if (!HasHighPiercingResistance(uid, false))
            return;

        SpawnSparkEffect(uid, component);
    }

    // Check if the damage contains piercing damage (typical for bullets)
    private bool IsHitscanDamage(DamageModifyEvent args) =>
        args.Damage.DamageDict.ContainsKey("Piercing") && args.Damage.DamageDict["Piercing"] > 0;

    // SP bullets have negative armor penetration (-0.25 to -1)
    // HP bullets have very negative armor penetration (-1)
    // This is a heuristic based on the hitscan prototypes we examined
    private bool IsSPOrHPBullet(DamageModifyEvent args) =>
        args.ArmorPenetration < 0;

    private void SpawnSparkEffect(EntityUid uid, ArmorSparkEffectComponent component, bool useParent = false)
    {
        var transform = Transform(uid);
        var target = useParent ? transform.ParentUid : uid;

        if (!Exists(target))
            return;

        var targetTransform = Transform(target);

        // Calculate random offset within the tile
        var offsetX = _random.NextFloat(-component.MaxOffset, component.MaxOffset);
        var offsetY = _random.NextFloat(-component.MaxOffset, component.MaxOffset);
        var offset = new Vector2(offsetX, offsetY);

        // Spawn the effect at the targets's position with offset
        var effectCoords = targetTransform.Coordinates.Offset(offset);

        SparkEffectAt(effectCoords, component.SparkEffectPrototype, component.RicochetSoundCollection);
    }

    private void SparkEffectAt(EntityCoordinates coordinates, string effectPrototype, string soundCollection)
    {
        SpawnSparkEffectAt(coordinates, effectPrototype);
        PlayRicochetSound(coordinates, soundCollection);
    }

    protected abstract void SpawnSparkEffectAt(EntityCoordinates coordinates, string effectPrototype);
    protected abstract void PlayRicochetSound(EntityCoordinates coordinates, string soundCollection);
}

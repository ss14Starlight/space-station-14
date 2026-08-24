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
    [Dependency] private IPrototypeManager _prototypeManager = default!;

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

    private void OnArmorDamageModify(EntityUid uid, ArmorSparkEffectComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        HandleSparkHit(uid, component, args.Args);
    }

    private bool HasHighPiercingResistance(EntityUid target)
    {
        // Determine who is actually being protected.
        var wearer = target;

        // If this is worn armor, its parent is the entity wearing it.
        var parent = Transform(target).ParentUid;

        if (TryComp<ArmorComponent>(target, out _) && Exists(parent))
            wearer = parent;

        // Get the combined Piercing coefficient of all worn armor.
        var wornQuery = new CoefficientQueryEvent(
            SlotFlags.HEAD |
            SlotFlags.EYES |
            SlotFlags.MASK |
            SlotFlags.OUTERCLOTHING |
            SlotFlags.INNERCLOTHING |
            SlotFlags.GLOVES |
            SlotFlags.FEET);

        var relayedEvent = new InventoryRelayedEvent<CoefficientQueryEvent>(
            wornQuery,
            wearer);

        RaiseLocalEvent(wearer, relayedEvent);

        var wornCoefficient =
            wornQuery.DamageModifiers.Coefficients.TryGetValue("Piercing", out var worn)
                ? worn
                : 1f;

        // Get innate Piercing resistance.
        var innateCoefficient = 1f;

        if (TryComp<DamageableComponent>(wearer, out var damageable) &&
            damageable.DamageModifierSetId != null &&
            _prototypeManager.Resolve(damageable.DamageModifierSetId, out var modifierSet) &&
            modifierSet.Coefficients.TryGetValue("Piercing", out var innate))
        {
            innateCoefficient = innate;
        }

        // Worn armor + innate armor stack multiplicatively.
        var totalCoefficient = wornCoefficient * innateCoefficient;

        // 0.2 coefficient = 80% damage resistance.
        return totalCoefficient <= 0.2f;
    }

    private void HandleSparkHit(EntityUid uid, ArmorSparkEffectComponent component, DamageModifyEvent args)
    {
        // Only process on server
        if (!_net.IsServer)
            return;

        // Check if this is a hitscan damage event
        if (!IsHitscanDamage(args))
            return;

        if (!IsSPOrHPBullet(args))
            return;

        // AlwaysSpark bypasses the armor calculation.
        if (!AlwaysSparks(uid, component) &&
            !HasHighPiercingResistance(uid))
            return;

        // The ONLY place where the spark is actually spawned.
        var useParent = TryComp<ArmorComponent>(uid, out _);

        SpawnSparkEffect(uid, component, useParent);
    }

    private bool IsHitscanDamage(DamageModifyEvent args)
    {
        // Check if the damage contains piercing damage (typical for bullets)
        return args.Damage.DamageDict.ContainsKey("Piercing") && args.Damage.DamageDict["Piercing"] > 0;
    }

    private bool IsSPOrHPBullet(DamageModifyEvent args)
    {
        // SP bullets have negative armor penetration (-0.25 to -1)
        // HP bullets have very negative armor penetration (-1)
        // This is a heuristic based on the hitscan prototypes we examined
        return args.ArmorPenetration < 0;
    }

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

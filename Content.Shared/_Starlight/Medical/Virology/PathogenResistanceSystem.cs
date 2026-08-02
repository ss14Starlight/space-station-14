using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;

namespace Content.Shared._Starlight.Medical.Virology;

/// <summary>
/// Evaluates pathogen PPE from the wearer's active internals, classified equipment, and
/// occupied clothing slots.
///
/// Virus transmission is inhalation-only: bottled air or a bio hood seals the airway,
/// while filter masks are imperfect. Supply masks and pressure helmets contain no filter
/// medium, so they do nothing with internals off.
///
/// Bacteria transmission is contact-driven: only a clean bio suit or sterile medical
/// gloves count. Hardsuits and ordinary work gloves are barriers, but not clean ones.
///
/// Fungus is environmental and additive: inhalation protection contributes one half and
/// protection against settling spores contributes the other.
/// </summary>
public sealed partial class PathogenResistanceSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;

    /// <summary>
    /// Slots containing classified PPE or contributing fungal body coverage.
    /// </summary>
    public const SlotFlags ProtectiveSlots =
        SlotFlags.FEET |
        SlotFlags.HEAD |
        SlotFlags.EYES |
        SlotFlags.GLOVES |
        SlotFlags.MASK |
        SlotFlags.INNERCLOTHING |
        SlotFlags.OUTERCLOTHING;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenResistanceComponent, InventoryRelayedEvent<PathogenResistanceQueryEvent>>(OnResistanceQuery);
        SubscribeLocalEvent<PathogenResistanceComponent, ExaminedEvent>(OnExamine);
    }

    private void OnResistanceQuery(Entity<PathogenResistanceComponent> ent, ref InventoryRelayedEvent<PathogenResistanceQueryEvent> query)
    {
        query.Args.Classes.Add(ent.Comp.Class);
    }

    private void OnExamine(Entity<PathogenResistanceComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(GetExamineKey(ent.Comp.Class)));
    }

    /// <summary>
    /// Returns the remaining infection chance coefficient after PPE. One is unprotected
    /// and zero is complete protection.
    /// </summary>
    public float GetResistance(EntityUid uid, Pathogen strain)
    {
        var ev = new PathogenResistanceQueryEvent(ProtectiveSlots);
        RaiseLocalEvent(uid, ev);

        var protection = PathogenProtectionMath.CalculateProtection(
            strain.PathogenType,
            _internals.AreInternalsWorking(uid),
            ev.Classes,
            GetFungalSlotProtection(uid));

        return PathogenProtectionMath.ApplyBypass(protection, strain.ProtectionBypass);
    }

    private float GetFungalSlotProtection(EntityUid uid)
    {
        return PathogenProtectionMath.FungalSlotProtection(
            HasItem(uid, "jumpsuit"),
            HasItem(uid, "outerClothing"),
            HasItem(uid, "shoes"),
            HasItem(uid, "gloves"),
            HasItem(uid, "head"),
            HasItem(uid, "eyes"));
    }

    private bool HasItem(EntityUid uid, string slot)
        => _inventory.TryGetSlotEntity(uid, slot, out _);

    private static string GetExamineKey(PathogenProtectionClass protectionClass)
    {
        return protectionClass switch
        {
            PathogenProtectionClass.FilterMask => "pathogen-protection-filter-mask",
            PathogenProtectionClass.SupplyMask => "pathogen-protection-supply-mask",
            PathogenProtectionClass.SterileBarrier => "pathogen-protection-sterile-barrier",
            PathogenProtectionClass.BioSuit => "pathogen-protection-bio-suit",
            PathogenProtectionClass.BioHood => "pathogen-protection-bio-hood",
            PathogenProtectionClass.SealedSuit => "pathogen-protection-sealed-suit",
            _ => "pathogen-protection-unknown",
        };
    }
}

/// <summary>
/// Deterministic PPE calculation kept separate from entity and inventory lookup.
/// </summary>
public static class PathogenProtectionMath
{
    private const float VirusFilterProtection = 0.90f;
    private const float BacteriaSterileGloveProtection = 0.90f;
    private const float FungusSealedSuitProtection = 0.45f;
    private const float FungusFilteredInhalationProtection = 0.40f;
    private const float FungusCompleteHalfProtection = 0.50f;

    public static float CalculateProtection(
        PathogenType type,
        bool internalsWorking,
        IReadOnlySet<PathogenProtectionClass> classes,
        float fungalSlotProtection)
    {
        var filterMask = classes.Contains(PathogenProtectionClass.FilterMask);
        var bioHood = classes.Contains(PathogenProtectionClass.BioHood);
        var bioSuit = classes.Contains(PathogenProtectionClass.BioSuit);
        var sealedSuit = classes.Contains(PathogenProtectionClass.SealedSuit);

        return type switch
        {
            PathogenType.Virus => internalsWorking || bioHood
                ? 1f
                : filterMask
                    ? VirusFilterProtection
                    : 0f,
            PathogenType.Bacteria => bioSuit
                ? 1f
                : classes.Contains(PathogenProtectionClass.SterileBarrier)
                    ? BacteriaSterileGloveProtection
                    : 0f,
            PathogenType.Fungus => Math.Clamp(
                (internalsWorking || bioHood
                    ? FungusCompleteHalfProtection
                    : filterMask
                        ? FungusFilteredInhalationProtection
                        : 0f) +
                (bioSuit
                    ? FungusCompleteHalfProtection
                    : sealedSuit
                        ? FungusSealedSuitProtection
                        : Math.Clamp(fungalSlotProtection, 0f, FungusFilteredInhalationProtection)),
                0f,
                1f),
            _ => 0f,
        };
    }

    public static float FungalSlotProtection(
        bool uniform,
        bool outerClothing,
        bool shoes,
        bool gloves,
        bool head,
        bool eyes)
    {
        var protection =
            (uniform ? 0.15f : 0f) +
            (outerClothing ? 0.10f : 0f) +
            (shoes ? 0.06f : 0f) +
            (gloves ? 0.04f : 0f) +
            (head ? 0.03f : 0f) +
            (eyes ? 0.02f : 0f);

        return Math.Min(FungusFilteredInhalationProtection, protection);
    }

    public static float ApplyBypass(float protection, float protectionBypass)
    {
        protection = Math.Clamp(protection, 0f, 1f);
        if (protection >= 1f)
            return 0f;

        var bypass = Math.Clamp(protectionBypass, 0f, 1f);
        return 1f - protection * (1f - bypass);
    }
}

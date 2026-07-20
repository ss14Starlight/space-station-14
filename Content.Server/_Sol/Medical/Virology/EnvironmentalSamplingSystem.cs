using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Environmental scraping for trait genes used by the bioterror lab.
/// </summary>
public sealed class EnvironmentalSamplingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EnvironmentalScraperComponent, AfterInteractEvent>(OnScraperInteract);
        SubscribeLocalEvent<EnvironmentalScraperComponent, EnvironmentalScrapeDoAfterEvent>(OnScrapeDoAfter);
    }

    private void OnScraperInteract(Entity<EnvironmentalScraperComponent> scraper, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Handled)
            return;

        EnsureSourceProfile(args.Target.Value);

        if (!TryComp<EnvironmentalMicrobeSourceComponent>(args.Target, out var source))
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-scrape-invalid"), args.Target.Value, args.User);
            return;
        }

        if (source.RemainingSamples <= 0 || source.NextAvailable > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("sol-bioterror-scrape-depleted"), args.Target.Value, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, scraper.Comp.ScrapeDelay, new EnvironmentalScrapeDoAfterEvent(), scraper, args.Target, scraper)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnScrapeDoAfter(Entity<EnvironmentalScraperComponent> scraper, ref EnvironmentalScrapeDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;

        if (!TryComp<EnvironmentalMicrobeSourceComponent>(args.Target, out var source))
            return;

        if (source.RemainingSamples <= 0 || source.NextAvailable > _timing.CurTime)
            return;

        source.RemainingSamples--;
        source.NextAvailable = _timing.CurTime + source.Cooldown;
        Dirty(args.Target.Value, source);

        var contaminated = !scraper.Comp.Sterile || scraper.Comp.Used;
        var user = args.User;
        var sample = Spawn("SolMicrobialSample", Transform(scraper).Coordinates);
        var sampleComp = EnsureComp<MicrobialSampleComponent>(sample);
        sampleComp.ChassisId = null;
        sampleComp.Quality = Math.Clamp(source.BaseQuality + _random.NextFloat(-0.15f, 0.15f), 0.05f, 1f);
        sampleComp.SourceLabel = MetaData(args.Target.Value).EntityName;
        sampleComp.Analyzed = false;

        if (contaminated)
        {
            sampleComp.Contaminated = true;
            sampleComp.Quality *= 0.5f;
        }

        // Always pick at least one trait when the source has a pool.
        if (source.TraitPool.Count > 0)
        {
            var trait = PickWeightedTrait(source.TraitPool);
            if (trait != null)
                sampleComp.Traits.Add(trait.Value);
        }

        // Chance of a second weak trait on high-quality sources.
        if (sampleComp.Quality > 0.8f && source.TraitPool.Count > 1 && _random.Prob(0.35f))
        {
            var trait = PickWeightedTrait(source.TraitPool);
            if (trait != null && !sampleComp.Traits.Contains(trait.Value))
                sampleComp.Traits.Add(trait.Value);
        }

        Dirty(sample, sampleComp);
        _meta.SetEntityName(sample, Loc.GetString("sol-bioterror-sample-name", ("source", sampleComp.SourceLabel ?? "unknown")));

        // Consume the scraper and place the sample in the freed hand.
        Del(scraper.Owner);
        _hands.PickupOrDrop(user, sample, checkActionBlocker: false);

        _popup.PopupEntity(Loc.GetString("sol-bioterror-scrape-success"), sample, user);
    }

    private ProtoId<PathogenTraitPrototype>? PickWeightedTrait(List<WeightedTraitEntry> pool)
    {
        var total = 0f;
        foreach (var entry in pool)
            total += Math.Max(0.01f, entry.Weight);

        var roll = _random.NextFloat(0f, total);
        var acc = 0f;
        foreach (var entry in pool)
        {
            acc += Math.Max(0.01f, entry.Weight);
            if (roll <= acc)
                return entry.Trait;
        }

        return pool[^1].Trait;
    }

    /// <summary>
    /// Lazily attaches a gene pool based on prototype / tags.
    /// </summary>
    public void EnsureSourceProfile(EntityUid target)
    {
        if (HasComp<EnvironmentalMicrobeSourceComponent>(target))
            return;

        var protoId = MetaData(target).EntityPrototype?.ID ?? string.Empty;
        EnvironmentalMicrobeSourceComponent? profile = null;

        if (protoId.Contains("Vent", StringComparison.OrdinalIgnoreCase) ||
            protoId.Contains("Scrubber", StringComparison.OrdinalIgnoreCase) ||
            protoId.Contains("AirAlarm", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile(0.7f, 3,
                ("SolTraitAirborne", 2f),
                ("SolTraitAerosolStable", 1.5f),
                ("SolTraitCoughShed", 1.2f),
                ("SolTraitSneezeShed", 1f),
                ("SolTraitDyspnea", 0.8f),
                ("SolTraitOrganLungs", 0.7f),
                ("SolTraitTreatAntiviral", 0.9f),
                ("SolTraitTreatRibavirin", 0.5f));
        }
        else if (protoId.Contains("Hydroponics", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Soil", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Planter", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile(0.75f, 4,
                ("SolTraitCultureYield", 2f),
                ("SolTraitIncubationSlow", 1f),
                ("SolTraitIngestion", 1.2f),
                ("SolTraitSneezeShed", 0.9f),
                ("SolTraitTreatAmoxla", 0.7f));
        }
        else if (protoId.Contains("Disposal", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Drain", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Trash", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Kitchen", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile(0.65f, 4,
                ("SolTraitIngestion", 2f),
                ("SolTraitEnvironmentalGrowth", 1.5f),
                ("SolTraitContact", 1f),
                ("SolTraitHemorrhage", 0.8f),
                ("SolTraitTreatCeftriaxone", 0.9f),
                ("SolTraitTreatAmoxla", 0.6f));
        }
        else if (HasComp<SurfaceContaminationComponent>(target) ||
                 protoId.Contains("Blood", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Med", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile(0.8f, 2,
                ("SolTraitVirulent", 2f),
                ("SolTraitOrganLiver", 1.2f),
                ("SolTraitOrganHeart", 1f),
                ("SolTraitHemorrhage", 1.1f),
                ("SolTraitContact", 1f),
                ("SolTraitTreatAntiviral", 1.2f),
                ("SolTraitTreatCeftriaxone", 1f),
                ("SolTraitTreatRibavirin", 0.7f));
        }
        else if (protoId.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Girder", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile(0.55f, 5,
                ("SolTraitContact", 2f),
                ("SolTraitPersistent", 1.5f),
                ("SolTraitSterilantResist", 0.8f),
                ("SolTraitOrganLiver", 0.6f),
                ("SolTraitTreatAmoxla", 0.5f));
        }

        if (profile == null)
            return;

        var comp = AddComp<EnvironmentalMicrobeSourceComponent>(target);
        comp.TraitPool = profile.TraitPool;
        comp.BaseQuality = profile.BaseQuality;
        comp.RemainingSamples = profile.RemainingSamples;
        comp.Cooldown = profile.Cooldown;
        Dirty(target, comp);
    }

    private static EnvironmentalMicrobeSourceComponent MakeProfile(
        float quality,
        int samples,
        params (ProtoId<PathogenTraitPrototype> Trait, float Weight)[] traits)
    {
        var profile = new EnvironmentalMicrobeSourceComponent
        {
            BaseQuality = quality,
            RemainingSamples = samples,
            Cooldown = TimeSpan.FromSeconds(20),
        };

        foreach (var (trait, weight) in traits)
        {
            profile.TraitPool.Add(new WeightedTraitEntry
            {
                Trait = trait,
                Weight = weight,
            });
        }

        return profile;
    }
}

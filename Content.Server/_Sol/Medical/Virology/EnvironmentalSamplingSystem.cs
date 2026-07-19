using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Sterile environmental scraping for microbial traits / chassis material.
/// </summary>
public sealed class EnvironmentalSamplingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
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

        var sample = Spawn("SolMicrobialSample", Transform(args.User).Coordinates);
        var sampleComp = EnsureComp<MicrobialSampleComponent>(sample);
        sampleComp.ChassisId = source.ChassisId;
        sampleComp.Quality = Math.Clamp(source.BaseQuality + _random.NextFloat(-0.15f, 0.15f), 0.05f, 1f);
        sampleComp.SourceLabel = MetaData(args.Target.Value).EntityName;
        sampleComp.Analyzed = false;

        if (!scraper.Comp.Sterile || scraper.Comp.Used)
        {
            sampleComp.Contaminated = true;
            sampleComp.Quality *= 0.5f;
        }

        // Weighted trait pick — may yield nothing useful.
        if (source.TraitPool.Count > 0 && _random.Prob(0.75f))
        {
            var trait = PickWeightedTrait(source.TraitPool);
            if (trait != null)
                sampleComp.Traits.Add(trait.Value);
        }

        // Chance of a second weak trait on high-quality sources.
        if (sampleComp.Quality > 0.8f && source.TraitPool.Count > 1 && _random.Prob(0.25f))
        {
            var trait = PickWeightedTrait(source.TraitPool);
            if (trait != null && !sampleComp.Traits.Contains(trait.Value))
                sampleComp.Traits.Add(trait.Value);
        }

        Dirty(sample, sampleComp);
        scraper.Comp.Used = true;
        scraper.Comp.Sterile = false;
        Dirty(scraper);
        _meta.SetEntityName(sample, Loc.GetString("sol-bioterror-sample-name", ("source", sampleComp.SourceLabel ?? "unknown")));
        _popup.PopupEntity(Loc.GetString("sol-bioterror-scrape-success"), sample, args.User);
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
    /// Lazily attaches a source profile based on prototype / tags.
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
            profile = MakeProfile("SolPathogenFlu", 0.7f, 3,
                ("SolTraitAirborne", 2f),
                ("SolTraitAerosolStable", 1.5f),
                ("SolTraitCoughShed", 1f));
        }
        else if (protoId.Contains("Hydroponics", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Soil", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Planter", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile("SolPathogenFlu", 0.75f, 4,
                ("SolTraitCultureYield", 2f),
                ("SolTraitIncubationSlow", 1f),
                ("SolTraitIngestion", 1.2f));
        }
        else if (protoId.Contains("Disposal", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Drain", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Trash", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Kitchen", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile("SolPathogenWoundSepsis", 0.65f, 4,
                ("SolTraitIngestion", 2f),
                ("SolTraitEnvironmentalGrowth", 1.5f),
                ("SolTraitContact", 1f));
        }
        else if (HasComp<SurfaceContaminationComponent>(target) ||
                 protoId.Contains("Blood", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile("SolPathogenWoundSepsis", 0.8f, 2,
                ("SolTraitVirulent", 2f),
                ("SolTraitOrganLiver", 1.2f),
                ("SolTraitContact", 1f));
        }
        else if (protoId.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                 protoId.Contains("Girder", StringComparison.OrdinalIgnoreCase))
        {
            profile = MakeProfile("SolPathogenWoundSepsis", 0.55f, 5,
                ("SolTraitContact", 2f),
                ("SolTraitPersistent", 1.5f),
                ("SolTraitSterilantResist", 0.8f));
        }

        if (profile == null)
            return;

        var comp = AddComp<EnvironmentalMicrobeSourceComponent>(target);
        comp.ChassisId = profile.ChassisId;
        comp.TraitPool = profile.TraitPool;
        comp.BaseQuality = profile.BaseQuality;
        comp.RemainingSamples = profile.RemainingSamples;
        comp.Cooldown = profile.Cooldown;
        Dirty(target, comp);
    }

    private static EnvironmentalMicrobeSourceComponent MakeProfile(
        ProtoId<PathogenPrototype> chassis,
        float quality,
        int samples,
        params (ProtoId<PathogenTraitPrototype> Trait, float Weight)[] traits)
    {
        var profile = new EnvironmentalMicrobeSourceComponent
        {
            ChassisId = chassis,
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

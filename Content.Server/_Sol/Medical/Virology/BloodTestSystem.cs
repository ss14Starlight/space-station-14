using System.Linq;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Blood draw into vials and centrifuge-driven pathogen / immunity panels.
/// </summary>
public sealed class BloodTestSystem : EntitySystem
{
    [Dependency] private readonly PathogenSystem _pathogen = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SolHealthAnalyzerSystem _analyzer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CentrifugeCompatibleBloodVialComponent, AfterInteractEvent>(OnVialInteract);
        SubscribeLocalEvent<CentrifugeCompatibleBloodVialComponent, ExaminedEvent>(OnVialExamined);
        SubscribeLocalEvent<ReactionMixerComponent, AfterMixingEvent>(OnAfterMix);
    }

    private void OnVialInteract(Entity<CentrifugeCompatibleBloodVialComponent> vial, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Handled)
            return;

        if (!HasComp<MobStateComponent>(args.Target) || !HasComp<BloodstreamComponent>(args.Target))
            return;

        if (HasComp<PathogenSampleComponent>(vial))
        {
            _popup.PopupEntity(Loc.GetString("sol-blood-vial-full"), vial, args.User);
            return;
        }

        var sample = EnsureComp<PathogenSampleComponent>(vial);
        sample.IsBloodSample = true;
        sample.IsCentrifuged = false;

        if (TryComp<PathogenCarrierComponent>(args.Target.Value, out var carrier) && carrier.Infections.Count > 0)
        {
            var infection = carrier.Infections[0];
            sample.PathogenId = infection.PathogenId;
            sample.Dose = infection.Dose;
            sample.DetectedStage = infection.Stage;
            sample.ForceNegative = infection.Stage == PathogenStage.Incubation && infection.Dose < 1.25f;
        }

        vial.Comp.SourceEntity = GetNetEntity(args.Target.Value);
        Dirty(vial.Owner, sample);
        Dirty(vial);
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("sol-blood-drawn", ("target", Identity.Entity(args.Target.Value, EntityManager))), vial, args.User);
    }

    private void OnAfterMix(Entity<ReactionMixerComponent> mixer, ref AfterMixingEvent args)
    {
        // AfterMixingEvent: Mixed=mixer machine, Mixer=target vial (upstream naming).
        var vial = args.Mixer;
        if (!TryComp<CentrifugeCompatibleBloodVialComponent>(vial, out var bloodVial))
            return;

        if (!TryComp<PathogenSampleComponent>(vial, out var sample) || !sample.IsBloodSample)
            return;

        // Only treat centrifuge-category mixers as blood panels.
        var isCentrifuge = false;
        foreach (var type in mixer.Comp.ReactionTypes)
        {
            if (type.Id != "Centrifuge")
                continue;
            isCentrifuge = true;
            break;
        }

        if (!isCentrifuge)
            return;

        sample.IsCentrifuged = true;
        bloodVial.PanelReady = true;
        Dirty(vial, sample);
        Dirty(vial, bloodVial);
    }

    private void OnVialExamined(Entity<CentrifugeCompatibleBloodVialComponent> vial, ref ExaminedEvent args)
    {
        if (!vial.Comp.PanelReady)
            return;

        args.PushMarkup(BuildBloodPanelText(vial));
    }

    public string BuildBloodPanelText(EntityUid vial)
    {
        if (!TryComp<PathogenSampleComponent>(vial, out var sample) || !sample.IsCentrifuged)
            return Loc.GetString("sol-blood-panel-not-ready");

        if (sample.ForceNegative || sample.PathogenId == null)
            return Loc.GetString("sol-blood-panel-pathogen-negative");

        if (!_pathogen.TryResolvePathogen(sample.PathogenId.Value, out var pathogen) || pathogen == null)
            return Loc.GetString("sol-blood-panel-inconclusive");

        var stage = sample.DetectedStage?.ToString() ?? "Unknown";
        var immunity = "None";
        var organFunction = Loc.GetString("sol-blood-panel-organs-unknown");
        if (TryComp<CentrifugeCompatibleBloodVialComponent>(vial, out var bloodVial) &&
            bloodVial.SourceEntity is { } netSource &&
            TryGetEntity(netSource, out var source))
        {
            var mult = _pathogen.GetImmunityMultiplier(source.Value, pathogen);
            immunity = mult <= 0.01f ? "Full" : mult < 1f ? $"Partial ({mult:F2})" : "None";
            organFunction = BuildOrganFunctionSummary(source.Value);
        }

        return Loc.GetString("sol-blood-panel-full",
            ("disease", pathogen.DisplayName),
            ("stage", stage),
            ("dose", sample.Dose.ToString("F1")),
            ("immunity", immunity),
            ("organs", organFunction));
    }

    private string BuildOrganFunctionSummary(EntityUid source)
    {
        if (!_body.GetBodyOrgans(source).Any())
            return Loc.GetString("sol-blood-panel-organs-none");

        var damaged = 0;
        var failing = 0;
        var missing = 0;
        foreach (var (_, _, status) in _analyzer.BuildOrganStatus(source))
        {
            switch (status)
            {
                case "Missing":
                    missing++;
                    break;
                case "Damaged":
                    damaged++;
                    break;
                case "Failing":
                case "Critical":
                    failing++;
                    break;
            }
        }

        return Loc.GetString("sol-blood-panel-organs-summary",
            ("damaged", damaged),
            ("failing", failing),
            ("missing", missing));
    }
}

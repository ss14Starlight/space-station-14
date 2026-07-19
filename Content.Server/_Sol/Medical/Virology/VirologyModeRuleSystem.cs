using System.Numerics;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives.Systems;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Sol.Medical.Virology.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Sol.Medical.Virology;

/// <summary>
/// Controllers for the Sol Virology game mode / bioterror cell selection.
/// </summary>
public sealed class VirologyModeRuleSystem : GameRuleSystem<VirologyModeRuleComponent>
{
    [Dependency] private readonly PathogenStrainRegistrySystem _strains = default!;
    [Dependency] private readonly CodeConditionSystem _code = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VirologyModeRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<VirologyModeRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
        SubscribeLocalEvent<BioterrorStrainSynthesizedEvent>(OnStrainSynthesized);
        SubscribeLocalEvent<BioterrorPayloadDeployedEvent>(OnPayloadDeployed);
        SubscribeLocalEvent<BioterrorVaccineCreatedEvent>(OnVaccineCreated);
    }

    protected override void Started(EntityUid uid, VirologyModeRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        EnsureComp<BioterrorCellTrackerComponent>(uid);
    }

    protected override void Ended(EntityUid uid, VirologyModeRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        _strains.Clear();
    }

    protected override void AppendRoundEndText(
        EntityUid uid,
        VirologyModeRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        if (!TryComp<BioterrorCellTrackerComponent>(uid, out var tracker))
            return;

        args.AddLine(Loc.GetString("sol-bioterror-roundend-header"));
        args.AddLine(Loc.GetString("sol-bioterror-roundend-lab",
            ("established", tracker.LabEstablishedOffShuttle),
            ("analyzer", tracker.AnalyzerDeployed),
            ("incubator", tracker.IncubatorDeployed),
            ("synthesizer", tracker.SynthesizerDeployed)));
        args.AddLine(Loc.GetString("sol-bioterror-roundend-strain",
            ("strain", tracker.SynthesizedStrainId ?? Loc.GetString("sol-bioterror-roundend-none"))));
        args.AddLine(Loc.GetString("sol-bioterror-roundend-deployed",
            ("load", tracker.DeployedLoad.ToString("F1")),
            ("required", tracker.RequiredDeployedLoad.ToString("F1"))));
        args.AddLine(Loc.GetString("sol-bioterror-roundend-medical",
            ("diagnosed", tracker.Diagnosed),
            ("vaccine", tracker.VaccineCreated)));
        var survivors = CountLivingCellMembers();
        args.AddLine(Loc.GetString("sol-bioterror-roundend-survivors", ("count", survivors)));
        if (survivors > 0)
            CompleteObjectiveForCell("BioterrorSurviveObjective");
    }

    private void OnRuleLoadedGrids(Entity<VirologyModeRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        if (args.Grids.Count == 0)
            return;

        var grid = args.Grids[0];
        var tracker = EnsureComp<BioterrorCellTrackerComponent>(ent);
        tracker.SpawnShuttleGrid = grid;

        var coords = new EntityCoordinates(grid, 0.5f, 0.5f);
        Spawn("SpawnPointHeadBioterrorist", coords);
        Spawn("SpawnPointBioterrorist", coords.Offset(new Vector2(1f, 0f)));
        Spawn("SpawnPointBioterrorist", coords.Offset(new Vector2(-1f, 0f)));
        Spawn("SpawnPointBioterrorist", coords.Offset(new Vector2(0f, 1f)));
    }

    private void OnAntagSelected(Entity<VirologyModeRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var bioterror = EnsureComp<BioterroristComponent>(args.EntityUid);
        bioterror.IsHead = args.Def.PrefRoles.Contains(new ProtoId<AntagPrototype>("HeadBioterrorist"));
        Dirty(args.EntityUid, bioterror);
    }

    private void OnStrainSynthesized(ref BioterrorStrainSynthesizedEvent args)
    {
        foreach (var tracker in EntityQuery<BioterrorCellTrackerComponent>())
        {
            tracker.SynthesizedStrainId = args.StrainId;
        }

        CompleteObjectiveForCell("BioterrorSynthesizeStrainObjective");
    }

    private void OnPayloadDeployed(ref BioterrorPayloadDeployedEvent args)
    {
        foreach (var tracker in EntityQuery<BioterrorCellTrackerComponent>())
        {
            tracker.DeployedLoad += args.Concentration;
            tracker.FirstDeploymentAt ??= Timing.CurTime;
            if (tracker.DeployedLoad >= tracker.RequiredDeployedLoad)
                CompleteObjectiveForCell("BioterrorDeployPayloadObjective");
        }
    }

    private void OnVaccineCreated(ref BioterrorVaccineCreatedEvent args)
    {
        foreach (var tracker in EntityQuery<BioterrorCellTrackerComponent>())
        {
            tracker.VaccineCreated = true;
            tracker.VaccineCreatedAt ??= Timing.CurTime;
            tracker.Diagnosed = true;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var tracker in EntityQuery<BioterrorCellTrackerComponent>())
        {
            if (tracker.LabEstablishedOffShuttle)
                CompleteObjectiveForCell("BioterrorEstablishLabObjective");

            if (tracker.FirstDeploymentAt != null &&
                tracker.VaccineCreatedAt == null &&
                Timing.CurTime - tracker.FirstDeploymentAt > tracker.DiagnosisDelayTarget)
            {
                CompleteObjectiveForCell("BioterrorDelayDiagnosisObjective");
            }
        }
    }

    private void CompleteObjectiveForCell(string objectiveProto)
    {
        var query = EntityQueryEnumerator<BioterroristComponent, MindContainerComponent>();
        while (query.MoveNext(out var uid, out _, out var mindContainer))
        {
            _code.SetCompleted((uid, mindContainer), objectiveProto);
        }
    }

    private int CountLivingCellMembers()
    {
        var count = 0;
        var query = EntityQueryEnumerator<BioterroristComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mob))
        {
            if (_mobState.IsAlive(uid, mob))
                count++;
        }

        return count;
    }
}

[RegisterComponent]
public sealed partial class VirologyModeRuleComponent : Component
{
    [DataField]
    public List<EntProtoId> DeniedRuleIds = new();
}

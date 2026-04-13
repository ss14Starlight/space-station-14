using System.Linq; // Starlight-edit
using Content.Server.Chat.Systems; // Starlight-edit
using Content.Server.Medical.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chat; // Starlight
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes; // Starlight-edit
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint; // Starlight
using Content.Shared.Hands.EntitySystems; // Starlight-edit
using Content.Shared.Humanoid; // Starlight-edit
using Content.Shared.Humanoid.Prototypes; // Starlight-edit
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper; // Starlight-edit
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared._Starlight.Time; // Starlight-edit
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes; // Starlight-edit
using Robust.Shared.Timing;
using Robust.Shared.Utility; // Starlight-edit
using Content.Server._Starlight.Medical.Body.Systems;

namespace Content.Server.Medical;

public sealed class HealthAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    // Starlight-start: Printable health reports.
    [Dependency] private readonly TimeSystem _timeSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    // Starlight-end

    [Dependency] private readonly ChatSystem _chat = default!; // Starlight-edit

    public override void Initialize()
    {
        SubscribeLocalEvent<HealthAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerPrintReportMessage>(OnPrintReportMessage); // Starlight-edit: Printable health reports.
        SubscribeLocalEvent<HealthAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<HealthAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<HealthAnalyzerComponent, DroppedEvent>(OnDropped);
    }

    public override void Update(float frameTime)
    {
        var analyzerQuery = EntityQueryEnumerator<HealthAnalyzerComponent, TransformComponent>();
        while (analyzerQuery.MoveNext(out var uid, out var component, out var transform))
        {
            //Update rate limited to 1 second
            if (component.NextUpdate > _timing.CurTime)
                continue;

            if (component.ScannedEntity is not {} patient)
                continue;

            if (Deleted(patient))
            {
                StopAnalyzingEntity((uid, component), patient);
                continue;
            }

            component.NextUpdate = _timing.CurTime + component.UpdateInterval;

            //Get distance between health analyzer and the scanned entity
            //null is infinite range
            var patientCoordinates = Transform(patient).Coordinates;
            if (component.MaxScanRange != null && !_transformSystem.InRange(patientCoordinates, transform.Coordinates, component.MaxScanRange.Value))
            {
                //Range too far, disable updates
                StopAnalyzingEntity((uid, component), patient);
                continue;
            }

            UpdateScannedUser(uid, patient, true);
        }
    }

    /// <summary>
    /// Trigger the doafter for scanning
    /// </summary>
    private void OnAfterInteract(Entity<HealthAnalyzerComponent> uid, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<MobStateComponent>(args.Target) || !_cell.HasDrawCharge(uid.Owner, user: args.User))
        	return;

        _audio.PlayPvs(uid.Comp.ScanningBeginSound, uid);

        var doAfterCancelled = !_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, uid.Comp.ScanDelay, new HealthAnalyzerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            NeedHand = true,
            BreakOnMove = true,
        });

        if (args.Target == args.User || doAfterCancelled || uid.Comp.Silent)
            return;

        var msg = Loc.GetString("health-analyzer-popup-scan-target", ("user", Identity.Entity(args.User, EntityManager)));
        _popupSystem.PopupEntity(msg, args.Target.Value, args.Target.Value, PopupType.Medium);
    }

    private void OnDoAfter(Entity<HealthAnalyzerComponent> uid, ref HealthAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null || !_cell.HasDrawCharge(uid.Owner, user: args.User))
            return;

        if (!uid.Comp.Silent)
            _audio.PlayPvs(uid.Comp.ScanningEndSound, uid);

        OpenUserInterface(args.User, uid);
        BeginAnalyzingEntity(uid, args.Target.Value);
        args.Handled = true;
    }
    // Starlight-start: Printable health reports.
    private void OnPrintReportMessage(Entity<HealthAnalyzerComponent> ent, ref HealthAnalyzerPrintReportMessage args)
    {
        if (ent.Comp.ScannedEntity is not { } patient)
        {
            _popupSystem.PopupEntity(Loc.GetString("health-analyzer-report-no-patient"), ent.Owner, args.Actor, PopupType.Medium);
            return;
        }

        if (_timing.CurTime < ent.Comp.PrintReadyAt)
        {
            UpdateScannedUser(ent.Owner, patient, true);
            return;
        }

        if (Deleted(patient) || !HasComp<DamageableComponent>(patient) || !HasComp<MobStateComponent>(patient))
        {
            ent.Comp.ScannedEntity = null;
            _uiSystem.ServerSendUiMessage(ent.Owner, HealthAnalyzerUiKey.Key, new HealthAnalyzerScannedUserMessage(new HealthAnalyzerUiState()));
            _popupSystem.PopupEntity(Loc.GetString("health-analyzer-report-invalid-patient"), ent.Owner, args.Actor, PopupType.Medium);
            return;
        }

        if (!_cell.HasDrawCharge(ent.Owner, user: args.Actor))
            return;

        PrintPatientReport(ent, args.Actor, patient);
        UpdateScannedUser(ent.Owner, patient, true);
    }
    // Starlight-end

    /// <summary>
    /// Turn off when placed into a storage item or moved between slots/hands
    /// </summary>
    private void OnInsertedIntoContainer(Entity<HealthAnalyzerComponent> uid, ref EntGotInsertedIntoContainerMessage args)
    {
        if (uid.Comp.ScannedEntity is { } patient)
            _toggle.TryDeactivate(uid.Owner);
    }

    /// <summary>
    /// Disable continuous updates once turned off
    /// </summary>
    private void OnToggled(Entity<HealthAnalyzerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated && ent.Comp.ScannedEntity is { } patient)
            StopAnalyzingEntity(ent, patient);
    }

    /// <summary>
    /// Turn off the analyser when dropped
    /// </summary>
    private void OnDropped(Entity<HealthAnalyzerComponent> uid, ref DroppedEvent args)
    {
        if (uid.Comp.ScannedEntity is { } patient)
            _toggle.TryDeactivate(uid.Owner);
    }

    private void OpenUserInterface(EntityUid user, EntityUid analyzer)
    {
        if (!_uiSystem.HasUi(analyzer, HealthAnalyzerUiKey.Key))
            return;

        _uiSystem.OpenUi(analyzer, HealthAnalyzerUiKey.Key, user);
    }

    /// <summary>
    /// Mark the entity as having its health analyzed, and link the analyzer to it
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that should receive the updates</param>
    /// <param name="target">The entity to start analyzing</param>
    public void BeginAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target) // Starlight-edit: Make it public, so we can reuse it in other systems
    {
        //Link the health analyzer to the scanned entity
        healthAnalyzer.Comp.ScannedEntity = target;

        _toggle.TryActivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, true);
    }

    /// <summary>
    /// Remove the analyzer from the active list, and remove the component if it has no active analyzers
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer that's receiving the updates</param>
    /// <param name="target">The entity to analyze</param>
    public void StopAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target) // Starlight-edit: Make it public, so we can reuse it in other systems
    {
        //Unlink the analyzer
        healthAnalyzer.Comp.ScannedEntity = null;

        _toggle.TryDeactivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, false);
    }

    /// <summary>
    /// Send an update for the target to the healthAnalyzer
    /// </summary>
    /// <param name="healthAnalyzer">The health analyzer</param>
    /// <param name="target">The entity being scanned</param>
    /// <param name="scanMode">True makes the UI show ACTIVE, False makes the UI show INACTIVE</param>
    public void UpdateScannedUser(EntityUid healthAnalyzer, EntityUid target, bool scanMode)
    {
        if (!_uiSystem.HasUi(healthAnalyzer, HealthAnalyzerUiKey.Key)
            || !HasComp<DamageableComponent>(target))
            return;

        var uiState = GetHealthAnalyzerUiState(target);
        // Starlight-start: Printable health reports.
        uiState.CanPrint = TryComp<HealthAnalyzerComponent>(healthAnalyzer, out var analyzerComp)
            && analyzerComp.ScannedEntity == target
            && _timing.CurTime >= analyzerComp.PrintReadyAt;
        // Starlight-end
        uiState.ScanMode = scanMode;

        // Starlight-start: Talking health analyzer
        if (TryComp<HealthAnalyzerComponent>(healthAnalyzer, out var healthComp)
            && healthComp.Talk
            && healthComp.NextTalk < _timing.CurTime
            && TryComp<DamageableComponent>(target, out var damageable)
            && scanMode
            )
        {
            healthComp.NextTalk = _timing.CurTime + healthComp.TalkInterval;

            var bloodLevel = !float.IsNaN(uiState.BloodLevel) ? $"{uiState.BloodLevel * 100:F1} %" : Loc.GetString("health-analyzer-window-entity-unknown-value-text");
            _chat.TrySendInGameICMessage(healthAnalyzer, Loc.GetString(healthComp.TalkMessage, ("damage", damageable.TotalDamage.ToString()), ("blood", bloodLevel)), InGameICChatType.Speak, hideChat: true);
        }
        // Starlight-end

        _uiSystem.ServerSendUiMessage(
            healthAnalyzer,
            HealthAnalyzerUiKey.Key,
            new HealthAnalyzerScannedUserMessage(uiState)
        );
    }

    /// <summary>
    /// Creates a HealthAnalyzerState based on the current state of an entity.
    /// </summary>
    /// <param name="target">The entity being scanned</param>
    /// <returns></returns>
    public HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target)
    {
        if (!target.HasValue || !HasComp<DamageableComponent>(target))
            return new HealthAnalyzerUiState();

        var entity = target.Value;
        var bodyTemperature = float.NaN;

        if (TryComp<TemperatureComponent>(entity, out var temp))
            bodyTemperature = temp.CurrentTemperature;

        var bloodAmount = float.NaN;
        var bleeding = false;
        var unrevivable = false;

        if (TryComp<BloodstreamComponent>(entity, out var bloodstream) &&
            _solutionContainerSystem.ResolveSolution(entity, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var bloodSolution))
        {
            bloodAmount = _bloodstreamSystem.GetBloodLevel(entity);
            bleeding = bloodstream.BleedAmount > 0;
        }

        if (TryComp<UnrevivableComponent>(entity, out var unrevivableComp) && unrevivableComp.Analyzable)
            unrevivable = true;

        // Starlight begin - Get a list of metabolizing chemicals
        List<(string ReagentId, FixedPoint2 Quantity)>? metabolizingReagents = null;
        if (TryComp<BloodstreamComponent>(entity, out var bloodstreamComp) &&
            _solutionContainerSystem.TryGetSolution(entity, bloodstreamComp.BloodSolutionName, out _, out var chemicalsSolution))
        {
            metabolizingReagents = new List<(string, FixedPoint2)>();
            foreach (var (reagent, quantity) in chemicalsSolution.Contents)
            {
                // Skip blood and only show actual chemicals being metabolized
                var isBlood = false;
                foreach (var (bloodReagent, _) in bloodstreamComp.BloodReferenceSolution.Contents)
                {
                    if (bloodReagent.Prototype == reagent.Prototype)
                    {
                        isBlood = true;
                        break;
                    }
                }
                if (isBlood)
                    continue;

                metabolizingReagents.Add((reagent.Prototype, quantity));
            }
        }
        // Starlight end

        return new HealthAnalyzerUiState(
            GetNetEntity(entity),
            bodyTemperature,
            bloodAmount,
            null, // Starlight-edit: Printable health reports.
            null,
            bleeding,
            unrevivable,
            metabolizingReagents // Starlight - add metabolizing chemicals to ui message
        );
    }

    // Starlight-start: Printable health reports.
    private void PrintPatientReport(Entity<HealthAnalyzerComponent> analyzer, EntityUid user, EntityUid patient)
    {
        var snapshot = BuildPatientSnapshot(patient);
        var paper = Spawn("Paper", Transform(user).Coordinates);

        if (!TryComp<PaperComponent>(paper, out var paperComp))
        {
            Del(paper);
            return;
        }

        paperComp.EditingDisabled = true;
        _handsSystem.PickupOrDrop(user, paper, checkActionBlocker: false);
        _metaData.SetEntityName(paper, Loc.GetString("health-analyzer-report-title", ("name", snapshot.RawName)));
        _paperSystem.SetContent((paper, paperComp), BuildReportMarkup(snapshot));
        _audio.PlayPvs(analyzer.Comp.PrintSound, analyzer);
        analyzer.Comp.PrintReadyAt = _timing.CurTime + analyzer.Comp.PrintCooldown;
    }

    private HealthAnalyzerPatientSnapshot BuildPatientSnapshot(EntityUid patient)
    {
        var uiState = GetHealthAnalyzerUiState(patient);
        var (shiftTime, _) = _timeSystem.GetStationTime();
        var entityName = HasComp<MetaDataComponent>(patient)
            ? Identity.Name(patient, EntityManager)
            : Loc.GetString("health-analyzer-window-entity-unknown-text");

        var species = Loc.GetString("health-analyzer-window-entity-unknown-species-text");
        if (TryComp<HumanoidAppearanceComponent>(patient, out var humanoidAppearanceComponent) &&
            _prototypeManager.TryIndex<SpeciesPrototype>(humanoidAppearanceComponent.Species, out var speciesPrototype))
        {
            species = Loc.GetString(speciesPrototype.Name);
        }

        var status = Loc.GetString("health-analyzer-window-entity-unknown-text");
        if (TryComp<MobStateComponent>(patient, out var mobStateComponent))
            status = HealthAnalyzerFormatting.GetStatusText(mobStateComponent.CurrentState);

        var damageable = Comp<DamageableComponent>(patient);
        IReadOnlyDictionary<string, FixedPoint2> damagePerType = damageable.Damage.DamageDict;
        var groupedInjuries = damageable.DamagePerGroup
            .OrderBy(group => HealthAnalyzerFormatting.GetDamageGroupSortKey(group.Key))
            .ThenBy(group => group.Key)
            .Select(group => BuildDamageGroupSnapshot(group.Key, group.Value, damagePerType))
            .Where(group => group != null)
            .Cast<HealthAnalyzerDamageGroupSnapshot>()
            .ToList();

        return new HealthAnalyzerPatientSnapshot(
            entityName,
            FormattedMessage.EscapeText(entityName),
            FormattedMessage.EscapeText(species),
            shiftTime.ToString(@"hh\:mm"),
            FormattedMessage.EscapeText(status),
            uiState.Temperature,
            uiState.BloodLevel,
            damageable.TotalDamage,
            groupedInjuries);
    }

    private HealthAnalyzerDamageGroupSnapshot? BuildDamageGroupSnapshot(
        string damageGroupId,
        FixedPoint2 damageAmount,
        IReadOnlyDictionary<string, FixedPoint2> damagePerType)
    {
        if (!_prototypeManager.TryIndex<DamageGroupPrototype>(damageGroupId, out var groupPrototype))
            return null;

        var types = new List<HealthAnalyzerDamageTypeSnapshot>();
        foreach (var typeId in groupPrototype.DamageTypes)
        {
            if (!damagePerType.TryGetValue(typeId, out var typeAmount) || typeAmount <= 0)
                continue;

            string localizedType = _prototypeManager.TryIndex<DamageTypePrototype>(typeId, out var typePrototype)
                ? typePrototype.LocalizedName
                : typeId.ToString();

            types.Add(new HealthAnalyzerDamageTypeSnapshot(FormattedMessage.EscapeText(localizedType), typeAmount));
        }

        if (damageAmount <= 0 && types.Count == 0)
            return null;

        return new HealthAnalyzerDamageGroupSnapshot(FormattedMessage.EscapeText(groupPrototype.LocalizedName), damageAmount, types);
    }

    private string BuildReportMarkup(HealthAnalyzerPatientSnapshot snapshot)
    {
        var message = new FormattedMessage();

        message.AddMarkupOrThrow($"[head=2][bold]{Loc.GetString("health-analyzer-report-section-patient")}[/bold][/head]");
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-patient-name", ("name", snapshot.Name)));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-patient-species", ("species", snapshot.Species)));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-patient-shift-time", ("time", snapshot.ShiftTime)));
        message.PushNewline();
        message.PushNewline();

        message.AddMarkupOrThrow($"[head=2][bold]{Loc.GetString("health-analyzer-report-section-summary")}[/bold][/head]");
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-summary-status", ("status", snapshot.Status)));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-summary-temperature", ("temperature", HealthAnalyzerFormatting.FormatTemperature(snapshot.Temperature))));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-summary-blood", ("blood", HealthAnalyzerFormatting.FormatBloodLevelMarkup(snapshot.BloodLevel))));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-summary-total-damage", ("damage", snapshot.TotalDamage)));
        message.PushNewline();
        message.PushNewline();

        message.AddMarkupOrThrow($"[head=2][bold]{Loc.GetString("health-analyzer-report-section-injuries")}[/bold][/head]");
        message.PushNewline();

        if (snapshot.DamageGroups.Count == 0)
        {
            message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-no-injuries"));
            return message.ToMarkup();
        }

        foreach (var group in snapshot.DamageGroups)
        {
            var severitySuffix = HealthAnalyzerFormatting.GetDamageSeveritySuffix((float) group.Amount);
            var amountText = string.IsNullOrEmpty(severitySuffix)
                ? group.Amount.ToString()
                : $"{group.Amount} {severitySuffix}";
            var groupLine = Loc.GetString(
                "health-analyzer-report-injury-group",
                ("group", group.Name),
                ("amount", amountText));
            message.AddMarkupOrThrow(HealthAnalyzerFormatting.WrapMarkupWithColor(
                groupLine,
                HealthAnalyzerFormatting.GetDamageSeverityColor((float) group.Amount)));
            message.PushNewline();

            foreach (var damageType in group.DamageTypes)
            {
                var damageLine = Loc.GetString(
                    "health-analyzer-report-injury-type",
                    ("type", damageType.Name),
                    ("amount", damageType.Amount));
                message.AddMarkupOrThrow($"- {damageLine}");
                message.PushNewline();
            }
        }

        return message.ToMarkup();
    }

    private sealed record HealthAnalyzerPatientSnapshot(
        string RawName,
        string Name,
        string Species,
        string ShiftTime,
        string Status,
        float Temperature,
        float BloodLevel,
        FixedPoint2 TotalDamage,
        List<HealthAnalyzerDamageGroupSnapshot> DamageGroups);

    private sealed record HealthAnalyzerDamageGroupSnapshot(
        string Name,
        FixedPoint2 Amount,
        List<HealthAnalyzerDamageTypeSnapshot> DamageTypes);

    private sealed record HealthAnalyzerDamageTypeSnapshot(string Name, FixedPoint2 Amount);
    // Starlight-end
}

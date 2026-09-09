using Content.Shared._Functional.TutorialServer;
using Content.Shared._Functional.TutorialServer.StarlightSurgery;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Interaction;
using Content.Server.Popups;
using Content.Shared.Standing;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Functional.TutorialServer.StarlightSurgery;

/// <summary>
/// Server-side Starlight-style surgery Bound UI for tutorial NPCs.
/// Opens when a <see cref="TutorialStarlightSurgeryToolComponent"/> is used on a
/// <see cref="TutorialStarlightSurgeryTargetComponent"/>.
/// </summary>
public sealed partial class TutorialStarlightSurgerySystem : SharedTutorialStarlightSurgerySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Open only via UtilityVerb so wrong-role players never see a selectable option.
        SubscribeLocalEvent<TutorialStarlightSurgeryToolComponent, GetVerbsEvent<UtilityVerb>>(OnToolGetVerbs);
        SubscribeLocalEvent<TutorialStarlightSurgeryTargetComponent, ComponentInit>(OnTargetInit);
        SubscribeLocalEvent<TutorialStarlightSurgeryTargetComponent, TutorialStarlightSurgeryDoAfterEvent>(OnDoAfter);

        Subs.BuiEvents<TutorialStarlightSurgeryTargetComponent>(TutorialStarlightSurgeryUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnSurgeryUiOpened);
            subs.Event<TutorialStarlightSurgeryStepChosenBuiMsg>(OnStepChosen);
        });
    }

    private void OnSurgeryUiOpened(Entity<TutorialStarlightSurgeryTargetComponent> ent, ref BoundUIOpenedEvent args)
    {
        TryAdvanceUiOpenedStep(args.Actor);
    }

    private void TryAdvanceUiOpenedStep(EntityUid user)
    {
        if (!TryComp<TutorialParticipantComponent>(user, out var part))
            return;

        if (part.StepComplete != TutorialStepComplete.StarlightSurgeryUiOpened)
            return;

        _tutorial.AdvanceSubGoal(user);
    }

    private void OnTargetInit(Entity<TutorialStarlightSurgeryTargetComponent> ent, ref ComponentInit args)
    {
        // Starlight requires lying down; put tutorial patients down automatically.
        _standing.Down(ent.Owner);
    }

    private void OnToolGetVerbs(Entity<TutorialStarlightSurgeryToolComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Target == args.User)
            return;

        if (!TryComp<TutorialStarlightSurgeryTargetComponent>(args.Target, out var surgeryTarget))
            return;

        // Verb is omitted entirely for the wrong tutorial role.
        if (!TutorialSurgeryRoleLock.IsInTutorialRole(EntityManager, args.User, surgeryTarget.RequiredRoleId))
            return;

        var target = args.Target;
        var user = args.User;
        args.Verbs.Add(new UtilityVerb
        {
            Act = () =>
            {
                _ui.OpenUi(target, TutorialStarlightSurgeryUiKey.Key, user);
                RefreshUI(target);
            },
            Text = Loc.GetString("tutorial-starlight-surgery-verb"),
            Message = Loc.GetString("tutorial-starlight-surgery-verb-message"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            IconEntity = GetNetEntity(ent),
            DoContactInteraction = true,
        });
    }

    private void OnStepChosen(
        Entity<TutorialStarlightSurgeryTargetComponent> ent,
        ref TutorialStarlightSurgeryStepChosenBuiMsg args)
    {
        var user = args.Actor;
        if (!TutorialSurgeryRoleLock.IsInTutorialRole(EntityManager, user, ent.Comp.RequiredRoleId))
        {
            _ui.CloseUi(ent.Owner, TutorialStarlightSurgeryUiKey.Key, user);
            return;
        }

        if (!TryValidateStep(ent, args.Part, args.Surgery, args.Step, user, out var surgery, out var step, out _))
            return;

        if (!IsLyingDown(ent))
        {
            _popup.PopupEntity(Loc.GetString("tutorial-starlight-surgery-need-lying"), user, user);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            TimeSpan.FromSeconds(step.Duration),
            new TutorialStarlightSurgeryDoAfterEvent
            {
                Part = args.Part,
                Surgery = args.Surgery,
                Step = args.Step,
            },
            ent.Owner,
            ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 2f,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(Entity<TutorialStarlightSurgeryTargetComponent> ent, ref TutorialStarlightSurgeryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!TryValidateStep(ent, args.Part, args.Surgery, args.Step, args.User, out var surgery, out var step, out var tool))
        {
            RefreshUI(ent);
            return;
        }

        args.Handled = true;

        // Consume the cybernetic implant from the surgeon's hand.
        if (step.Tool == TutorialStarlightSurgeryToolType.EyeImplant)
        {
            QueueDel(tool);
            ent.Comp.HasEyeImplant = true;
            EnsureComp<EyeProtectionComponent>(ent.Owner);
            Dirty(ent);
        }

        var stepKey = $"{surgery.ID}:{step.Id}";
        ent.Comp.CompletedSteps.Add(stepKey);

        var isFinal = surgery.Steps[^1].Id == step.Id;
        if (!isFinal)
            ent.Comp.StartedSurgeries.Add(surgery.ID);
        else
        {
            ent.Comp.StartedSurgeries.Remove(surgery.ID);
            ent.Comp.CompletedSurgeries.Add(surgery.ID);

            if (surgery.GrantsEyeImplant)
                ent.Comp.HasEyeImplant = true;

            if (surgery.ClearsIncisionProgress)
            {
                if (ent.Comp.HasEyeImplant)
                    ent.Comp.ExampleSurgeryComplete = true;

                ClearIncisionProgress(ent.Comp);
            }
        }

        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("tutorial-starlight-surgery-step-done", ("step", step.Name)), args.User, args.User);
        RefreshUI(ent);
    }

    private void ClearIncisionProgress(TutorialStarlightSurgeryTargetComponent target)
    {
        // Keep implant surgeries; clear open/close incision bookkeeping like Starlight's clear progress.
        target.CompletedSteps.RemoveWhere(s =>
            s.StartsWith("TutorialSurgeryOpenIncision:") ||
            s.StartsWith("TutorialSurgeryCloseIncisionHead:"));
        target.CompletedSurgeries.Remove("TutorialSurgeryOpenIncision");
        target.CompletedSurgeries.Remove("TutorialSurgeryCloseIncisionHead");
        target.StartedSurgeries.Remove("TutorialSurgeryOpenIncision");
        target.StartedSurgeries.Remove("TutorialSurgeryCloseIncisionHead");
    }

    private bool TryValidateStep(
        Entity<TutorialStarlightSurgeryTargetComponent> ent,
        string part,
        string surgeryId,
        string stepId,
        EntityUid user,
        out TutorialStarlightSurgeryPrototype surgery,
        out TutorialStarlightSurgeryStepData step,
        out EntityUid tool)
    {
        surgery = default!;
        step = default!;
        tool = default;

        if (!ent.Comp.Parts.Contains(part))
            return false;

        if (!Proto.TryIndex(surgeryId, out surgery!))
            return false;

        if (!surgery.Parts.Contains(part))
            return false;

        if (!IsSurgeryAvailable(ent.Comp, surgery) &&
            !ent.Comp.CompletedSurgeries.Contains(surgery.ID) &&
            !ent.Comp.StartedSurgeries.Contains(surgery.ID))
            return false;

        step = surgery.Steps.Find(s => s.Id == stepId)!;
        if (step == null)
            return false;

        var next = GetNextStepIndex(ent.Comp, surgery);
        if (next is not { } nextIdx || surgery.Steps[nextIdx].Id != stepId)
        {
            _popup.PopupEntity(Loc.GetString("tutorial-starlight-surgery-wrong-step"), user, user);
            return false;
        }

        if (!TryFindHeldTool(user, step.Tool, out tool))
        {
            _popup.PopupEntity(Loc.GetString("tutorial-starlight-surgery-missing-tool", ("tool", step.Tool.ToString())),
                user, user);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Immediately completes a surgery step if it is the next valid step (integration tests).
    /// When <paramref name="skipToolCheck"/> is true, held tools are not required.
    /// </summary>
    public bool TryForceCompleteStep(
        EntityUid patient,
        EntityUid surgeon,
        string part,
        string surgeryId,
        string stepId,
        bool skipToolCheck = false)
    {
        if (!TryComp<TutorialStarlightSurgeryTargetComponent>(patient, out var target))
            return false;

        var ent = new Entity<TutorialStarlightSurgeryTargetComponent>(patient, target);
        if (!Proto.TryIndex(surgeryId, out TutorialStarlightSurgeryPrototype? surgery))
            return false;

        if (!ent.Comp.Parts.Contains(part) || !surgery.Parts.Contains(part))
            return false;

        if (!IsSurgeryAvailable(ent.Comp, surgery) &&
            !ent.Comp.CompletedSurgeries.Contains(surgery.ID) &&
            !ent.Comp.StartedSurgeries.Contains(surgery.ID))
            return false;

        var step = surgery.Steps.Find(s => s.Id == stepId);
        if (step == null)
            return false;

        var next = GetNextStepIndex(ent.Comp, surgery);
        if (next is not { } nextIdx || surgery.Steps[nextIdx].Id != stepId)
            return false;

        if (!skipToolCheck)
        {
            if (!TryFindHeldTool(surgeon, step.Tool, out var tool))
                return false;

            if (step.Tool == TutorialStarlightSurgeryToolType.EyeImplant)
                QueueDel(tool);
        }

        if (step.Tool == TutorialStarlightSurgeryToolType.EyeImplant)
        {
            ent.Comp.HasEyeImplant = true;
            EnsureComp<EyeProtectionComponent>(ent.Owner);
        }

        ent.Comp.CompletedSteps.Add($"{surgery.ID}:{step.Id}");
        var isFinal = surgery.Steps[^1].Id == step.Id;
        if (!isFinal)
            ent.Comp.StartedSurgeries.Add(surgery.ID);
        else
        {
            ent.Comp.StartedSurgeries.Remove(surgery.ID);
            ent.Comp.CompletedSurgeries.Add(surgery.ID);
            if (surgery.GrantsEyeImplant)
                ent.Comp.HasEyeImplant = true;
            if (surgery.ClearsIncisionProgress)
            {
                if (ent.Comp.HasEyeImplant)
                    ent.Comp.ExampleSurgeryComplete = true;
                ClearIncisionProgress(ent.Comp);
            }
        }

        Dirty(ent);
        RefreshUI(ent);
        return true;
    }

    public void RefreshUI(EntityUid body)
    {
        if (!TryComp<TutorialStarlightSurgeryTargetComponent>(body, out var target))
            return;

        var choices = new Dictionary<string, List<(string, string, bool)>>();
        foreach (var part in target.Parts)
        {
            var list = new List<(string, string, bool)>();
            foreach (var surgery in Proto.EnumeratePrototypes<TutorialStarlightSurgeryPrototype>())
            {
                if (!surgery.Parts.Contains(part))
                    continue;

                var completed = target.CompletedSurgeries.Contains(surgery.ID);
                if (!completed && !IsSurgeryAvailable(target, surgery) && !target.StartedSurgeries.Contains(surgery.ID))
                    continue;

                list.Add((surgery.ID, string.Empty, completed));
            }

            list.Sort((a, b) =>
            {
                var pa = Proto.Index<TutorialStarlightSurgeryPrototype>(a.Item1).Priority;
                var pb = Proto.Index<TutorialStarlightSurgeryPrototype>(b.Item1).Priority;
                var cmp = pa.CompareTo(pb);
                return cmp != 0 ? cmp : string.Compare(a.Item1, b.Item1, StringComparison.Ordinal);
            });

            choices[part] = list;
        }

        _ui.SetUiState(body, TutorialStarlightSurgeryUiKey.Key, new TutorialStarlightSurgeryBuiState
        {
            Choices = choices,
            IsLyingDown = IsLyingDown(body),
        });
    }
}

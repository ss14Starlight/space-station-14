using Content.Client._Starlight;
using Content.Client.Hands.Systems;
using Content.Client._Starlight.Medical.Surgery;
using Content.Client._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared._Starlight;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Surgery;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Control;

namespace Content.Client._Sol.Medical.Surgery;

/// <summary>
/// Contextual surgery UI: pick a body part, then choose the next tool-inferred action.
/// Does not reveal a future step checklist. Reuses Starlight surgery messages/validation.
/// </summary>
[UsedImplicitly]
public sealed class SolContextualSurgeryBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly StarlightEntitySystem _entitySystem;
    private readonly SurgerySystem _system;
    private readonly HandsSystem _hands;
    private readonly InventorySystem _inventory;
    private readonly PathogenSystem _pathogen;

    private SolContextualSurgeryWindow? _window;
    private EntityUid? _part;
    private SurgeryBuiState? _state;

    private NetEntity? _pendingPart;
    private EntProtoId? _pendingSurgery;
    private EntProtoId? _pendingStep;

    public SolContextualSurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = _entities.System<SurgerySystem>();
        _hands = _entities.System<HandsSystem>();
        _entitySystem = _entities.System<StarlightEntitySystem>();
        _inventory = _entities.System<InventorySystem>();
        _pathogen = _entities.System<PathogenSystem>();

        _hands.OnPlayerItemAdded += OnHandsChanged;
        _hands.OnPlayerItemRemoved += OnHandsChanged;
    }

    private void OnHandsChanged(string _, EntityUid __)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        RefreshActions();
        RefreshAsepsisBanner();
    }

    protected override void Open()
    {
        base.Open();
        UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (state is SurgeryBuiState s)
            Update(s);
    }

    private void Update(SurgeryBuiState state)
    {
        _state = state;
        TryInitWindow();

        _window!.Parts.DisposeAllChildren();
        _window.Actions.DisposeAllChildren();
        HideConfirm();
        ViewParts();

        var oldPart = _part;
        _part = null;

        var parts = new List<Entity<BodyPartComponent>>(state.Choices.Keys.Count);
        foreach (var choice in state.Choices.Keys)
        {
            if (_entities.TryGetEntity(choice, out var ent) &&
                _entities.TryGetComponent(ent, out BodyPartComponent? part))
            {
                parts.Add((ent.Value, part));
            }
        }

        parts.Sort(static (a, b) => GetPartScore(a.Comp.PartType).CompareTo(GetPartScore(b.Comp.PartType)));

        foreach (var part in parts)
        {
            var netPart = _entities.GetNetEntity(part.Owner);
            var surgeries = state.Choices[netPart];
            var partName = _entities.GetComponent<MetaDataComponent>(part).EntityName;
            var button = new ChoiceControl();
            button.Set(partName, null);
            button.Button.OnPressed += _ => OnPartPressed(netPart, surgeries);
            _window.Parts.AddChild(button);

            if (oldPart == part.Owner)
                OnPartPressed(netPart, surgeries);
        }

        RefreshAsepsisBanner();
        UpdateDisabledPanel();

        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private static int GetPartScore(BodyPartType type) => type switch
    {
        BodyPartType.Head => 1,
        BodyPartType.Torso => 2,
        BodyPartType.Arm => 3,
        BodyPartType.Hand => 4,
        BodyPartType.Leg => 5,
        BodyPartType.Foot => 6,
        BodyPartType.Tail => 7,
        BodyPartType.Other => 8,
        _ => 0,
    };

    private void TryInitWindow()
    {
        if (_window != null)
            return;

        _window = new SolContextualSurgeryWindow();
        _window.OnClose += Close;
        _window.Title = Loc.GetString("sol-surgery-window-title");

        _window.PartsButton.OnPressed += _ =>
        {
            _part = null;
            HideConfirm();
            ViewParts();
        };

        _window.ConfirmYesButton.OnPressed += _ =>
        {
            if (_pendingPart is not { } part ||
                _pendingSurgery is not { } surgery ||
                _pendingStep is not { } step)
            {
                HideConfirm();
                return;
            }

            SendStep(part, surgery, step);
            HideConfirm();
        };

        _window.ConfirmNoButton.OnPressed += _ => HideConfirm();
    }

    private void OnPartPressed(NetEntity netPart, List<(EntProtoId, string, bool)> surgeryIds)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        HideConfirm();
        PopulateActions(netPart, surgeryIds);
        ViewActions();
        RefreshAsepsisBanner();
        UpdateDisabledPanel();
    }

    private void PopulateActions(NetEntity netPart, List<(EntProtoId, string, bool)> surgeryIds)
    {
        if (_window == null || _part == null)
            return;

        _window.Actions.DisposeAllChildren();

        if (!_entities.TryGetComponent(_part, out BodyPartComponent? partComp))
            return;

        var partName = _entities.GetComponent<MetaDataComponent>(_part.Value).EntityName;
        _window.PartLabel.Text = partName;

        var seen = new HashSet<(EntProtoId Surgery, EntProtoId Step)>();
        var actions = new List<ContextualAction>();

        foreach (var (surgeryId, suffix, isCompleted) in surgeryIds)
        {
            if (isCompleted)
                continue;

            if (!_entitySystem.TryGetSingleton(surgeryId, out var surgeryEnt) ||
                !_entities.HasComponent<SurgeryComponent>(surgeryEnt))
            {
                continue;
            }

            var next = _system.GetNextStep(Owner, _part.Value, surgeryEnt);
            if (next == null)
                continue;

            var nextSurgery = next.Value.Surgery;
            var nextSurgeryProto = _entities.GetComponentOrNull<MetaDataComponent>(nextSurgery.Owner)?.EntityPrototype?.ID;
            if (nextSurgeryProto == null)
                continue;

            EntProtoId nextSurgeryId = nextSurgeryProto;
            var stepId = nextSurgery.Comp.Steps[next.Value.Step];
            if (!seen.Add((nextSurgeryId, stepId)))
                continue;

            if (!_entitySystem.TryGetSingleton(stepId, out var stepEnt))
                continue;

            var targetSurgeryName = $"{_entities.GetComponent<MetaDataComponent>(surgeryEnt).EntityName} {suffix}".Trim();
            var nextSurgeryName = _entities.GetComponent<MetaDataComponent>(nextSurgery.Owner).EntityName;
            var isPrerequisite = nextSurgery.Owner != surgeryEnt;

            actions.Add(new ContextualAction(
                nextSurgery.Comp.Priority,
                isPrerequisite
                    ? Loc.GetString("sol-surgery-action-prerequisite",
                        ("procedure", nextSurgeryName),
                        ("goal", targetSurgeryName))
                    : targetSurgeryName,
                stepEnt,
                stepId,
                netPart,
                nextSurgeryId));
        }

        actions.Sort((a, b) =>
        {
            var p = a.Priority.CompareTo(b.Priority);
            return p != 0 ? p : string.Compare(a.SurgeryName, b.SurgeryName, StringComparison.Ordinal);
        });

        if (actions.Count == 0)
        {
            _window.Actions.AddChild(new Label
            {
                Text = Loc.GetString("sol-surgery-no-actions"),
                HorizontalAlignment = HAlignment.Center,
            });
            return;
        }

        foreach (var action in actions)
            AddActionButton(action, partComp.PartType);
    }

    private void AddActionButton(ContextualAction action, BodyPartType partType)
    {
        if (_window == null || _player.LocalEntity is not { } player)
            return;

        var stepName = _entities.GetComponent<MetaDataComponent>(action.StepEnt).EntityName;
        var stepDesc = _entities.GetComponent<MetaDataComponent>(action.StepEnt).EntityDescription;
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow($"[bold]{FormattedMessage.EscapeText(action.SurgeryName)}[/bold]\n{FormattedMessage.EscapeText(stepName)}");

        var dirty = HasDirtyHeldTools(player);
        var infectionContext = IsVirologyContext();
        var canPerform = _system.CanPerformStep(
            player,
            Owner,
            partType,
            action.StepEnt,
            false,
            out var popup,
            out var reason,
            out _);

        if (!canPerform)
            msg.AddMarkupOrThrow($"\n[color=red]{FormattedMessage.EscapeText(FormatInvalidReason(reason, popup))}[/color]");
        else if (dirty)
        {
            msg.AddMarkupOrThrow($"\n[color=orange]{Loc.GetString(infectionContext
                ? "sol-surgery-action-dirty-tools"
                : "sol-surgery-action-dirty-tools-sterility")}[/color]");
        }

        var button = new ChoiceControl();
        var texture = _entities.GetComponentOrNull<SpriteComponent>(action.StepEnt)?.Icon?.Default;
        button.Set(msg, texture);
        button.Button.Disabled = !canPerform;
        button.Button.ToolTip = string.IsNullOrEmpty(stepDesc) ? stepName : stepDesc;

        button.Button.OnPressed += _ =>
        {
            if (dirty)
            {
                PromptDirtyConfirm(action.Part, action.SurgeryId, action.StepId, action.SurgeryName, stepName);
                return;
            }

            SendStep(action.Part, action.SurgeryId, action.StepId);
        };

        _window.Actions.AddChild(button);
    }

    private static string FormatInvalidReason(StepInvalidReason reason, string? popup)
    {
        return reason switch
        {
            StepInvalidReason.NeedsOperatingTable => Loc.GetString("sol-surgery-needs-table"),
            StepInvalidReason.Armor => Loc.GetString("sol-surgery-remove-armor"),
            StepInvalidReason.MissingTool => Loc.GetString("sol-surgery-missing-tool"),
            StepInvalidReason.DisabledTool => Loc.GetString("sol-surgery-disabled-tool"),
            StepInvalidReason.TooHigh => Loc.GetString("sol-surgery-item-too-high"),
            StepInvalidReason.NotEnoughReagent => Loc.GetString("sol-surgery-missing-reagent"),
            StepInvalidReason.MissingLimb => Loc.GetString("sol-surgery-missing-limb"),
            _ => popup ?? Loc.GetString("sol-surgery-cannot-perform"),
        };
    }

    private bool HasDirtyHeldTools(EntityUid player)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (_entities.TryGetComponent(held, out SurgicalToolSterilityComponent? sterility) &&
                sterility.State != SurgicalSterilityState.Sterile)
            {
                return true;
            }
        }

        return false;
    }

    private void PromptDirtyConfirm(NetEntity part, EntProtoId surgery, EntProtoId step, string surgeryName, string stepName)
    {
        if (_window == null)
            return;

        _pendingPart = part;
        _pendingSurgery = surgery;
        _pendingStep = step;
        _window.ConfirmLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString(
            IsVirologyContext() ? "sol-surgery-dirty-confirm-infection" : "sol-surgery-dirty-confirm",
            ("surgery", surgeryName),
            ("step", stepName))));
        _window.ConfirmPanel.Visible = true;
    }

    private void HideConfirm()
    {
        _pendingPart = null;
        _pendingSurgery = null;
        _pendingStep = null;
        if (_window != null)
            _window.ConfirmPanel.Visible = false;
    }

    private void SendStep(NetEntity part, EntProtoId surgery, EntProtoId step)
    {
        SendMessage(new SurgeryStepChosenBuiMsg
        {
            Part = part,
            Surgery = surgery,
            Step = step,
        });
    }

    private void RefreshActions()
    {
        if (_window == null || _part == null || _state == null)
            return;

        if (!_entities.TryGetNetEntity(_part, out var netPart) ||
            !_state.Choices.TryGetValue(netPart.Value, out var surgeries))
        {
            return;
        }

        PopulateActions(netPart.Value, surgeries);
    }

    private void RefreshAsepsisBanner()
    {
        if (_window == null)
            return;

        if (_player.LocalEntity is not { } player)
        {
            _window.AsepsisLabel.SetMessage(string.Empty);
            return;
        }

        var dirty = 0;
        var sterile = 0;
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (!_entities.TryGetComponent(held, out SurgicalToolSterilityComponent? sterility))
                continue;

            if (sterility.State == SurgicalSterilityState.Sterile)
                sterile++;
            else
                dirty++;
        }

        // Mask / infection framing only on virology stations; otherwise just tool cleanliness.
        if (IsVirologyContext())
        {
            var masked = _inventory.TryGetSlotEntity(player, "mask", out var mask) &&
                         _entities.HasComponent<SurgicalMaskProtectionComponent>(mask.Value);

            _window.AsepsisLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString(
                "sol-surgery-asepsis-banner",
                ("dirty", dirty),
                ("sterile", sterile),
                ("masked", masked))));
            return;
        }

        _window.AsepsisLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString(
            "sol-surgery-tool-banner",
            ("dirty", dirty),
            ("sterile", sterile))));
    }

    private bool IsVirologyContext()
    {
        if (_pathogen.IsVirologyEnabledAt(Owner))
            return true;

        return _player.LocalEntity is { } player && _pathogen.IsVirologyEnabledAt(player);
    }

    private void UpdateDisabledPanel()
    {
        if (_window == null)
            return;

        if (_system.IsLyingDown(Owner))
        {
            _window.DisabledPanel.Visible = false;
            _window.DisabledPanel.MouseFilter = MouseFilterMode.Ignore;
            return;
        }

        _window.DisabledPanel.Visible = true;
        if (_window.DisabledLabel.GetMessage() is null)
        {
            var text = new FormattedMessage();
            text.AddMarkupOrThrow(Loc.GetString("sol-surgery-must-lie-down"));
            _window.DisabledLabel.SetMessage(text);
        }

        _window.DisabledPanel.MouseFilter = MouseFilterMode.Stop;
    }

    private void ViewParts()
    {
        if (_window == null)
            return;

        _window.Parts.Visible = true;
        _window.Actions.Visible = false;
        _window.PartsButton.Disabled = true;
        _window.PartLabel.Text = string.Empty;
        _window.Title = Loc.GetString("sol-surgery-window-title");
    }

    private void ViewActions()
    {
        if (_window == null)
            return;

        _window.Parts.Visible = false;
        _window.Actions.Visible = true;
        _window.PartsButton.Disabled = false;

        if (_part != null && _entities.TryGetComponent(_part, out MetaDataComponent? meta))
            _window.Title = Loc.GetString("sol-surgery-window-title-part", ("part", meta.EntityName));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Close();
            _window = null;
        }

        _hands.OnPlayerItemAdded -= OnHandsChanged;
        _hands.OnPlayerItemRemoved -= OnHandsChanged;
    }

    private readonly record struct ContextualAction(
        int Priority,
        string SurgeryName,
        EntityUid StepEnt,
        EntProtoId StepId,
        NetEntity Part,
        EntProtoId SurgeryId);
}

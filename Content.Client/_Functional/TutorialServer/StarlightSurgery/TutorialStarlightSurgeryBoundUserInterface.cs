using System.Linq;
using Content.Client._Starlight;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Hands.Systems;
using Content.Shared._Functional.TutorialServer.StarlightSurgery;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Functional.TutorialServer.StarlightSurgery;

/// <summary>
/// Client Bound UI mirroring Starlight's Parts → Surgeries → Steps surgery window.
/// Backed by tutorial prototypes rather than a full body/organ surgery sim.
/// </summary>
[UsedImplicitly]
public sealed partial class TutorialStarlightSurgeryBoundUserInterface : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly TutorialStarlightSurgerySystem _system;
    private readonly HandsSystem _hands;

    private TutorialStarlightSurgeryWindow? _window;
    private string? _part;
    private string? _surgeryId;
    private readonly List<string> _previousSurgeries = new();

    public TutorialStarlightSurgeryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _system = _entities.System<TutorialStarlightSurgerySystem>();
        _hands = _entities.System<HandsSystem>();
        _hands.OnPlayerItemAdded += OnPlayerItemAdded;
    }

    private void OnPlayerItemAdded(string _, EntityUid __)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        RefreshSteps();
    }

    protected override void Open()
    {
        base.Open();
        UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
        if (state is TutorialStarlightSurgeryBuiState s)
            Update(s);
    }

    private void Update(TutorialStarlightSurgeryBuiState state)
    {
        TryInitWindow();

        _window!.Surgeries.DisposeAllChildren();
        _window.Steps.DisposeAllChildren();
        _window.Parts.DisposeAllChildren();
        View(ViewType.Parts);

        var oldSurgery = _surgeryId;
        var oldPart = _part;
        _part = null;
        _surgeryId = null;

        foreach (var (partId, surgeries) in state.Choices.OrderBy(p => PartSort(p.Key)))
        {
            var partButton = new ChoiceControl();
            partButton.Set(partId, null);
            partButton.Button.OnPressed += _ => OnPartPressed(partId, surgeries);
            _window.Parts.AddChild(partButton);

            if (oldPart == partId && oldSurgery != null &&
                surgeries.Any(s => s.SurgeryId == oldSurgery))
            {
                OnSurgeryPressed(partId, oldSurgery);
            }
            else if (oldPart == partId && oldSurgery == null)
            {
                OnPartPressed(partId, surgeries);
            }
        }

        UpdateDisabledPanel(state.IsLyingDown);

        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private static int PartSort(string part) => part switch
    {
        "Head" => 1,
        "Torso" => 2,
        "Arm" => 3,
        "Hand" => 4,
        "Leg" => 5,
        "Foot" => 6,
        _ => 99,
    };

    private void TryInitWindow()
    {
        if (_window != null)
            return;

        _window = new TutorialStarlightSurgeryWindow
        {
            Title = Loc.GetString("tutorial-starlight-surgery-window-title"),
        };
        _window.OnClose += Close;

        _window.PartsButton.OnPressed += _ =>
        {
            _part = null;
            _surgeryId = null;
            _previousSurgeries.Clear();
            View(ViewType.Parts);
        };

        _window.SurgeriesButton.OnPressed += _ =>
        {
            _surgeryId = null;
            _previousSurgeries.Clear();
            if (_part == null || State is not TutorialStarlightSurgeryBuiState s ||
                !s.Choices.TryGetValue(_part, out var surgeries))
                return;

            OnPartPressed(_part, surgeries);
        };

        _window.StepsButton.OnPressed += _ =>
        {
            if (_part == null || _previousSurgeries.Count == 0)
                return;

            var last = _previousSurgeries[^1];
            _previousSurgeries.RemoveAt(_previousSurgeries.Count - 1);
            OnSurgeryPressed(_part, last);
        };
    }

    private void OnPartPressed(string partId, List<(string SurgeryId, string Suffix, bool IsCompleted)> surgeryIds)
    {
        if (_window == null)
            return;

        _part = partId;
        _window.Surgeries.DisposeAllChildren();

        var surgeries = new List<(TutorialStarlightSurgeryPrototype Proto, string Name, bool IsCompleted)>();
        foreach (var (surgeryId, suffix, isCompleted) in surgeryIds)
        {
            if (!_proto.TryIndex<TutorialStarlightSurgeryPrototype>(surgeryId, out var surgery))
                continue;

            var name = string.IsNullOrEmpty(suffix) ? surgery.Name : $"{surgery.Name} {suffix}";
            surgeries.Add((surgery, name, isCompleted));
        }

        surgeries.Sort((a, b) =>
        {
            var priority = a.Proto.Priority.CompareTo(b.Proto.Priority);
            return priority != 0 ? priority : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        foreach (var (proto, name, isCompleted) in surgeries)
        {
            var button = new ChoiceControl();
            button.Set(name, null);
            if (isCompleted)
                button.Button.Modulate = Color.Green;
            button.Button.OnPressed += _ => OnSurgeryPressed(partId, proto.ID);
            _window.Surgeries.AddChild(button);
        }

        View(ViewType.Surgeries);
    }

    private void OnSurgeryPressed(string partId, string surgeryId)
    {
        if (_window == null || !_proto.TryIndex<TutorialStarlightSurgeryPrototype>(surgeryId, out var surgery))
            return;

        _part = partId;
        _surgeryId = surgeryId;
        _window.Steps.DisposeAllChildren();

        if (surgery.Requirements.Count > 0)
        {
            foreach (var requirementId in surgery.Requirements)
            {
                if (!_proto.TryIndex(requirementId, out TutorialStarlightSurgeryPrototype? requirement))
                    continue;

                var label = new ChoiceControl();
                var msg = new FormattedMessage();
                msg.AddMarkupOrThrow($"[bold]Requires: {requirement.Name}[/bold]");
                label.Set(msg, null);
                label.Button.OnPressed += _ =>
                {
                    _previousSurgeries.Add(surgeryId);
                    OnSurgeryPressed(partId, requirement.ID);
                };
                _window.Steps.AddChild(label);
                _window.Steps.AddChild(new HSeparator(Color.FromHex("#4972A1")) { Margin = new Thickness(0, 0, 0, 1) });
            }
        }

        foreach (var step in surgery.Steps)
        {
            var stepButton = new TutorialStarlightSurgeryStepButton
            {
                StepId = step.Id,
                TooltipTextSupplier = () => string.IsNullOrEmpty(step.Description) ? step.Name : step.Description,
            };
            stepButton.Button.OnPressed += _ => SendMessage(new TutorialStarlightSurgeryStepChosenBuiMsg
            {
                Part = partId,
                Surgery = surgeryId,
                Step = step.Id,
            });
            _window.Steps.AddChild(stepButton);
        }

        View(ViewType.Steps);
        RefreshSteps();
    }

    private void RefreshSteps()
    {
        if (_window == null ||
            _part == null ||
            _surgeryId == null ||
            !_proto.TryIndex<TutorialStarlightSurgeryPrototype>(_surgeryId, out var surgery) ||
            !_entities.TryGetComponent(Owner, out TutorialStarlightSurgeryTargetComponent? target))
            return;

        UpdateDisabledPanel(_system.IsLyingDown(Owner));

        var next = _system.GetNextStepIndex(target, surgery);
        var i = 0;
        foreach (var child in _window.Steps.Children)
        {
            if (child is not TutorialStarlightSurgeryStepButton stepButton)
                continue;

            if (i >= surgery.Steps.Count)
                break;

            var step = surgery.Steps[i];
            var status = StepStatus.Incomplete;
            if (next == null)
                status = StepStatus.Complete;
            else if (next.Value == i)
                status = StepStatus.Next;
            else if (i < next.Value)
                status = StepStatus.Complete;

            stepButton.Button.Disabled = status != StepStatus.Next;

            var stepName = new FormattedMessage();
            stepName.AddText(step.Name);

            if (status == StepStatus.Complete)
            {
                stepButton.Button.Modulate = Color.Green;
            }
            else if (status == StepStatus.Next)
            {
                stepButton.Button.Modulate = Color.White;
                if (_player.LocalEntity is { } player &&
                    !_system.TryFindHeldTool(player, step.Tool, out _))
                {
                    stepButton.Button.Disabled = true;
                    stepName.AddMarkupOrThrow(" [color=red](Missing tool)[/color]");
                    stepButton.TooltipTextSupplier = () => Loc.GetString(
                        "tutorial-starlight-surgery-missing-tool",
                        ("tool", step.Tool.ToString()));
                }
                else
                {
                    stepButton.TooltipTextSupplier = () =>
                        string.IsNullOrEmpty(step.Description) ? step.Name : step.Description;
                }
            }

            stepButton.Set(stepName, null);
            i++;
        }
    }

    private void UpdateDisabledPanel(bool lyingDown)
    {
        if (_window == null)
            return;

        if (lyingDown)
        {
            _window.DisabledPanel.Visible = false;
            _window.DisabledPanel.MouseFilter = Control.MouseFilterMode.Ignore;
            return;
        }

        _window.DisabledPanel.Visible = true;
        if (_window.DisabledLabel.GetMessage() is null)
        {
            var text = new FormattedMessage();
            text.AddMarkupOrThrow($"[color=red][font size=16]{Loc.GetString("tutorial-starlight-surgery-need-lying")}[/font][/color]");
            _window.DisabledLabel.SetMessage(text);
        }

        _window.DisabledPanel.MouseFilter = Control.MouseFilterMode.Stop;
    }

    private void View(ViewType type)
    {
        if (_window == null)
            return;

        _window.Parts.Visible = type == ViewType.Parts;
        _window.PartsButton.Disabled = type == ViewType.Parts;

        _window.Surgeries.Visible = type == ViewType.Surgeries;
        // Enabled on Steps so players can step back to the surgery list for the selected part.
        _window.SurgeriesButton.Disabled = type != ViewType.Steps;

        _window.Steps.Visible = type == ViewType.Steps;
        _window.StepsButton.Disabled = type != ViewType.Steps || _previousSurgeries.Count == 0;

        if (_part != null && _surgeryId != null &&
            _proto.TryIndex<TutorialStarlightSurgeryPrototype>(_surgeryId, out var surgery))
            _window.Title = Loc.GetString("tutorial-starlight-surgery-window-title-detail",
                ("part", _part), ("surgery", surgery.Name));
        else if (_part != null)
            _window.Title = Loc.GetString("tutorial-starlight-surgery-window-title-part", ("part", _part));
        else
            _window.Title = Loc.GetString("tutorial-starlight-surgery-window-title");
    }

    private enum ViewType : byte
    {
        Parts,
        Surgeries,
        Steps,
    }

    private enum StepStatus : byte
    {
        Next,
        Complete,
        Incomplete,
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Close();
        _hands.OnPlayerItemAdded -= OnPlayerItemAdded;
    }
}

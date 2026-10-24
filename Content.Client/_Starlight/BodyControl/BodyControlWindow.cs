using System.Linq;
using Content.Client.Actions;
using Content.Client._Starlight.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Shared._Starlight.BodyControl;
using Content.Shared.Hands.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.BodyControl;

public sealed class BodyControlWindow : FancyWindow
{
    public readonly record struct HandEntry(string Id, HandLocation Location, EntityUid? Item, string Name,
        EntProtoId? EmptyRepresentative = null);
    public event Action<BodyControlCommandEvent>? OnCommand;
    public event Action<EntityUid>? OnAction;
    private readonly IEntityManager _entities;
    private readonly ActionsSystem _actions;
    private readonly BoxContainer _hands = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly GridContainer _buttons = new() { Columns = 4 };
    private readonly Button _combat = new() { Text = Loc.GetString("body-control-panel-combat"), ToggleMode = true };
    private readonly List<Button> _shortcuts = new();
    private List<HandEntry> _shownHands = new();
    private List<EntityUid> _shownActions = new();
    private string? _serverHand;
    private string? _selectedHand;

    public BodyControlWindow(IEntityManager entities)
    {
        _entities = entities;
        _actions = entities.System<ActionsSystem>();
        Title = Loc.GetString("body-control-panel-title");
        MinSize = new(320, 220);
        SetSize = new(360, 420);
        var contents = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        contents.AddChild(new Label { Text = Loc.GetString("body-control-panel-hands") });
        contents.AddChild(_hands);
        var shortcuts = new BoxContainer { SeparationOverride = 4 };
        foreach (var (key, command) in new[] { ("use", BodyControlCommand.UseHand), ("activate", BodyControlCommand.ActivateHand), ("drop", BodyControlCommand.Drop) })
        {
            var button = new Button { Text = Loc.GetString($"body-control-panel-{key}"), HorizontalExpand = true, Disabled = true };
            button.OnPressed += _ => OnCommand?.Invoke(new BodyControlCommandEvent(command, _selectedHand));
            _shortcuts.Add(button);
            shortcuts.AddChild(button);
        }
        contents.AddChild(shortcuts);
        _combat.OnPressed += _ => OnCommand?.Invoke(new BodyControlCommandEvent(BodyControlCommand.ToggleCombat));
        contents.AddChild(_combat);
        contents.AddChild(new Label { Text = Loc.GetString("body-control-panel-actions") });
        contents.AddChild(_buttons);
        ContentsContainer.AddChild(new ScrollContainer { HScrollEnabled = false, Children = { contents } });
    }

    public void UpdateState(List<HandEntry> hands, List<EntityUid> actions, string? selectedHand, bool? combat)
    {
        _combat.Visible = combat != null;
        _combat.Pressed = combat ?? false;
        if (!_shownHands.SequenceEqual(hands) || _serverHand != selectedHand)
        {
            _shownHands = hands;
            _serverHand = selectedHand;
            _selectedHand = selectedHand;
            _hands.RemoveAllChildren();
            foreach (var hand in hands)
            {
                var row = new BoxContainer { SeparationOverride = 6 };
                var button = RemoteControlButtons.CreateHand(hand.Id, hand.Location, hand.Item,
                    hand.EmptyRepresentative, hand.Id == selectedHand, (args, _) =>
                {
                    if (args.Function != EngineKeyFunctions.UIClick)
                        return;
                    _selectedHand = hand.Id;
                    foreach (var entry in _hands.Children.OfType<BoxContainer>().SelectMany(entry => entry.Children).OfType<HandButton>())
                        entry.Highlight = entry.SlotName == hand.Id;
                    foreach (var shortcut in _shortcuts)
                        shortcut.Disabled = false;
                    OnCommand?.Invoke(new BodyControlCommandEvent(BodyControlCommand.SelectHand, hand.Id));
                    args.Handle();
                });
                row.AddChild(button);
                row.AddChild(new Label { Text = hand.Name, VerticalAlignment = VAlignment.Center });
                _hands.AddChild(row);
            }
            foreach (var shortcut in _shortcuts)
                shortcut.Disabled = !hands.Any(hand => hand.Id == selectedHand);
        }
        if (_shownActions.SequenceEqual(actions))
            return;
        _shownActions = actions;
        _buttons.RemoveAllChildren();
        foreach (var action in actions)
        {
            var button = RemoteControlButtons.CreateAction(_entities, _actions, action, false, (args, _) =>
            {
                if (args.Function != EngineKeyFunctions.UIClick)
                    return;
                OnAction?.Invoke(action);
                args.Handle();
            });
            _buttons.AddChild(button);
        }
    }
}

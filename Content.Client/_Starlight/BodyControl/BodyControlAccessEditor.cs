using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.BodyControl;

/// <summary>Edits one participant's access to one body; shared by the collective and debug menus.</summary>
public sealed class BodyControlAccessEditor : BoxContainer
{
    public event Action<BodyControlAccess?>? OnApply;
    private readonly BoxContainer _permissions = new() { Orientation = LayoutOrientation.Vertical };
    private readonly BoxContainer _hands = new() { Orientation = LayoutOrientation.Vertical };
    private readonly Dictionary<string, CheckBox> _permissionButtons = new();
    private readonly Dictionary<string, CheckBox> _handButtons = new();
    private BodyControlAccessState? _last;

    public BodyControlAccessEditor()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        AddChild(new Label { Text = Loc.GetString("body-control-access-title") });
        foreach (var permission in BodyControlAccess.StandardPermissions)
        {
            var button = new CheckBox { Text = Loc.GetString("body-control-access-" + permission[5..]) };
            _permissionButtons.Add(permission, button);
            _permissions.AddChild(button);
        }
        AddChild(_permissions);
        AddChild(new Label { Text = Loc.GetString("body-control-access-hands") });
        AddChild(_hands);
        var buttons = new BoxContainer { SeparationOverride = 6 };
        var apply = new Button { Text = Loc.GetString("body-control-access-apply"), HorizontalExpand = true };
        var reset = new Button { Text = Loc.GetString("body-control-access-reset"), HorizontalExpand = true };
        apply.OnPressed += _ => OnApply?.Invoke(ReadAccess());
        reset.OnPressed += _ =>
        {
            Reset();
            OnApply?.Invoke(null);
        };
        buttons.AddChild(apply);
        buttons.AddChild(reset);
        AddChild(buttons);
    }

    public void Reset() => _last = null;

    public void UpdateState(BodyControlAccessState state)
    {
        if (_last != null && _last.Access.SameAs(state.Access) && _last.Hands.SequenceEqual(state.Hands))
            return;
        _last = new(state.Access.Copy(), state.Hands.ToList());
        foreach (var (permission, button) in _permissionButtons)
            button.Pressed = state.Access.Allows(permission);
        _hands.RemoveAllChildren();
        _handButtons.Clear();
        foreach (var hand in state.Hands)
        {
            var button = new CheckBox
            {
                Text = hand,
                Pressed = state.Access.FullControl || state.Access.Hands == null || state.Access.Hands.Contains(hand),
            };
            _handButtons.Add(hand, button);
            _hands.AddChild(button);
        }
    }

    private BodyControlAccess ReadAccess()
    {
        var permissions = _permissionButtons.Where(pair => pair.Value.Pressed).Select(pair => pair.Key).ToHashSet();
        if (_last != null)
            permissions.UnionWith(_last.Access.Permissions.Except(BodyControlAccess.StandardPermissions));
        var hands = _handButtons.Where(pair => pair.Value.Pressed).Select(pair => pair.Key).ToHashSet();
        var allHands = hands.Count == _handButtons.Count;
        return new BodyControlAccess
        {
            FullControl = BodyControlAccess.StandardPermissions.All(permissions.Contains) && allHands
                && _last?.Access.Parts == null && _last?.Access.Actions == null,
            Permissions = permissions,
            Hands = allHands ? null : hands,
            Parts = _last?.Access.Parts == null ? null : new(_last.Access.Parts),
            Actions = _last?.Access.Actions == null ? null : new(_last.Access.Actions),
        };
    }
}

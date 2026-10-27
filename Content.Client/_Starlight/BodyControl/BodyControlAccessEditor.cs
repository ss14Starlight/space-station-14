using System.Linq;
using Content.Shared._Starlight.BodyControl;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.BodyControl;

/// <summary>Edits one participant's access to one body; shared by the collective and debug menus.</summary>
public sealed class BodyControlAccessEditor : BoxContainer
{
    public event Action<BodyControlAccess?>? OnApply;
    private readonly CheckBox _fullControl = new() { Text = Loc.GetString("body-control-access-full") };
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
        AddChild(_fullControl);
        _fullControl.OnToggled += _ => UpdateEnabled();
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
        _fullControl.Pressed = state.Access.FullControl;
        foreach (var (permission, button) in _permissionButtons)
            button.Pressed = state.Access.Permissions.Contains(permission);
        _hands.RemoveAllChildren();
        _handButtons.Clear();
        foreach (var hand in state.Hands)
        {
            var button = new CheckBox
            {
                Text = hand,
                Pressed = state.Access.Hands == null || state.Access.Hands.Contains(hand),
            };
            _handButtons.Add(hand, button);
            _hands.AddChild(button);
        }
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        foreach (var button in _permissionButtons.Values.Concat(_handButtons.Values))
            button.Disabled = _fullControl.Pressed;
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
            FullControl = _fullControl.Pressed,
            Permissions = permissions,
            Hands = allHands ? null : hands,
            Parts = _last?.Access.Parts == null ? null : new(_last.Access.Parts),
            Actions = _last?.Access.Actions == null ? null : new(_last.Access.Actions),
        };
    }
}

using System.Numerics;
using Content.Client._Starlight.BodyControl;
using Content.Client.Eui;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.MindCollective;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;

namespace Content.Client._Starlight.MindCollective;

[UsedImplicitly]
public sealed class MindCollectiveAdminEui : BaseEui
{
    private readonly MindCollectiveAdminWindow _window = new();

    public MindCollectiveAdminEui()
    {
        _window.OnRequest += SendMessage;
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened() => _window.OpenCentered();

    public override void Closed()
    {
        _window.Dispose();
        base.Closed();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is MindCollectiveAdminState menu)
            _window.UpdateState(menu);
    }
}

public sealed class MindCollectiveAdminWindow : FancyWindow
{
    public event Action<MindCollectiveAdminMessage>? OnRequest;

    private readonly OptionButton _groups = new() { HorizontalExpand = true };
    private readonly OptionButton _players = new() { HorizontalExpand = true };
    private readonly OptionButton _bodies = new() { HorizontalExpand = true };
    private readonly OptionButton _roles = new() { HorizontalExpand = true };
    private readonly RichTextLabel _feedback = new() { HorizontalExpand = true };
    private readonly BodyControlAccessEditor _accessEditor = new() { Visible = false };
    private NetUserId? _accessPlayer;
    private NetEntity? _accessBody;
    private readonly Dictionary<MindCollectiveAdminAction, Button> _buttons = new();
    private MindCollectiveAdminState? _state;
    private NetEntity? _group;
    private NetUserId? _player;
    private NetEntity? _body;
    private string? _role = "MindCollectiveOperator";

    public MindCollectiveAdminWindow()
    {
        Title = Loc.GetString("mind-collective-admin-title");
        MinSize = new(400, 440);
        SetSize = Vector2.Max(MinSize, Vector2.Min(new(520, 650), UserInterfaceManager.WindowRoot.Size - new Vector2(24)));
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, HorizontalExpand = true };
        ContentsContainer.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { content } });
        var hint = new RichTextLabel { HorizontalExpand = true };
        hint.SetMessage(Loc.GetString("mind-collective-admin-hint"));
        content.AddChild(hint);
        AddSelector(content, "mind-collective-admin-collective", _groups);
        AddSelector(content, "mind-collective-admin-player", _players);
        AddButtons(content, MindCollectiveAdminAction.Create, MindCollectiveAdminAction.Invite);
        AddSelector(content, "mind-collective-admin-role", _roles);
        AddButtons(content, MindCollectiveAdminAction.GrantRole, MindCollectiveAdminAction.RevokeRole);
        AddSelector(content, "mind-collective-admin-body", _bodies);
        AddButtons(content, MindCollectiveAdminAction.AddBody, MindCollectiveAdminAction.RemoveBody);
        AddButtons(content, MindCollectiveAdminAction.SpawnBody);
        content.AddChild(_accessEditor);
        _accessEditor.OnApply += access => OnRequest?.Invoke(new MindCollectiveAdminMessage(
            access == null ? MindCollectiveAdminAction.ResetAccess : MindCollectiveAdminAction.SaveAccess,
            _group, _player, _body, _role, access));
        AddButtons(content, MindCollectiveAdminAction.Enter, MindCollectiveAdminAction.Observe);
        AddButtons(content, MindCollectiveAdminAction.Control, MindCollectiveAdminAction.Leave);
        AddButtons(content, MindCollectiveAdminAction.Refresh);
        content.AddChild(_feedback);

        _groups.OnItemSelected += args =>
        {
            _groups.SelectId(args.Id);
            _group = args.Id > 0 ? _state!.Collectives[args.Id - 1].Entity : null;
            Request(MindCollectiveAdminAction.Refresh);
        };
        _players.OnItemSelected += args =>
        {
            _players.SelectId(args.Id);
            _player = args.Id > 0 ? _state!.Players[args.Id - 1].User : null;
            Request(MindCollectiveAdminAction.Refresh);
        };
        _bodies.OnItemSelected += args =>
        {
            _bodies.SelectId(args.Id);
            _body = args.Id > 0 ? _state!.Bodies[args.Id - 1].Entity : null;
            Request(MindCollectiveAdminAction.Refresh);
        };
        _roles.OnItemSelected += args =>
        {
            _roles.SelectId(args.Id);
            _role = args.Id > 0 ? _state!.Roles[args.Id - 1].Id : null;
        };
    }

    public void UpdateState(MindCollectiveAdminState state)
    {
        _state = state;
        _group = state.Collective;
        _player = state.Player;
        _body = state.Body;
        _accessEditor.Visible = state.Access != null;
        if (_accessPlayer != state.Player || _accessBody != state.Body)
        {
            _accessEditor.Reset();
            _accessPlayer = state.Player;
            _accessBody = state.Body;
        }
        if (state.Access is { } settings)
            _accessEditor.UpdateState(settings);
        Fill(_groups, state.Collectives, entry => entry.Name, entry => entry.Entity == _group);
        Fill(_players, state.Players, entry => entry.Name, entry => entry.User == _player);
        Fill(_bodies, state.Bodies, entry => entry.Name, entry => entry.Entity == _body);
        Fill(_roles, state.Roles, entry => entry.Assigned
            ? Loc.GetString("mind-collective-admin-assigned", ("role", entry.Name)) : entry.Name, entry => entry.Id == _role);
        _feedback.SetMessage(state.Feedback);
        foreach (var (action, button) in _buttons)
        {
            button.Disabled = action switch
            {
                MindCollectiveAdminAction.Refresh => false,
                MindCollectiveAdminAction.Create => _player == null,
                MindCollectiveAdminAction.AddBody or MindCollectiveAdminAction.RemoveBody => _group == null || _body == null,
                MindCollectiveAdminAction.GrantRole or MindCollectiveAdminAction.RevokeRole => _group == null || _player == null || _role == null,
                MindCollectiveAdminAction.Enter or MindCollectiveAdminAction.Observe or MindCollectiveAdminAction.Control => _group == null || _player == null || _body == null,
                _ => _group == null || _player == null,
            };
        }
    }

    private static void AddSelector(BoxContainer content, string label, OptionButton selector)
    {
        content.AddChild(new Label { Text = Loc.GetString(label) });
        content.AddChild(selector);
    }

    private void AddButtons(BoxContainer content, params MindCollectiveAdminAction[] actions)
    {
        var row = new BoxContainer { SeparationOverride = 6, HorizontalExpand = true };
        foreach (var action in actions)
        {
            var button = new Button
            {
                Text = Loc.GetString("mind-collective-admin-action-" + action.ToString().ToLowerInvariant()),
                HorizontalExpand = true,
                MinHeight = 32,
                Disabled = true,
            };
            button.OnPressed += _ => Request(action);
            _buttons.Add(action, button);
            row.AddChild(button);
        }
        content.AddChild(row);
    }

    private void Request(MindCollectiveAdminAction action)
        => OnRequest?.Invoke(new MindCollectiveAdminMessage(action, _group, _player, _body, _role));

    private static void Fill<T>(OptionButton button, List<T> items, Func<T, string> name, Predicate<T> selected)
    {
        button.Clear();
        button.AddItem(Loc.GetString("mind-collective-admin-select"), 0);
        for (var i = 0; i < items.Count; i++)
            button.AddItem(name(items[i]), i + 1);
        button.SelectId(items.FindIndex(selected) + 1);
    }
}

using System.Linq;
using System.Numerics;
using Content.Client._Starlight.BodyControl;
using Content.Shared._Starlight.BodyControl;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.MindCollective;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.MindCollective;

public sealed class MindCollectiveWindow : FancyWindow
{
    public event Action<NetEntity>? OnEnter;
    public event Action<NetEntity>? OnObserve;
    public event Action<NetEntity>? OnRecall;
    public event Action<NetEntity>? OnControl;
    public event Action<NetEntity, ProtoId<MindCollectiveRolePrototype>, bool>? OnSetRole;
    public event Action? OnLeave;
    public event Action<NetEntity, uint, bool>? OnExpel;
    public event Action<NetEntity?, NetEntity?>? OnConfigureControl;
    public event Action<NetEntity, NetEntity, BodyControlAccess?>? OnSetControl;
    private FancyWindow? _controlEditorWindow;
    private BodyControlAccessEditor? _controlEditor;
    private readonly OptionButton _controlBody = new() { HorizontalExpand = true };
    private MindCollectiveControlEditorState? _editing;
    private List<MindCollectiveBodyEntry> _controlBodies = new();
    private bool _closingControl;

    private readonly BoxContainer _bodies = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly BoxContainer _members = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly Button _leave = new() { Text = Loc.GetString("mind-collective-leave"), MinHeight = 32 };
    private List<MindCollectiveBodyEntry> _lastBodies = new();
    private List<MindCollectiveMemberEntry> _lastMembers = new();
    private bool? _lastCanManage;
    private bool? _lastMindspace;
    private FancyWindow? _confirmation;
    private uint _expelRequest;

    public MindCollectiveWindow()
    {
        Title = Loc.GetString("mind-collective-title");
        MinSize = new(350, 300);
        SetSize = Vector2.Max(MinSize, Vector2.Min(new(480, 500), UserInterfaceManager.WindowRoot.Size - new Vector2(24)));
        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        content.AddChild(_leave);
        content.AddChild(Text(Loc.GetString("mind-collective-chat-hint")));
        var tabs = new TabContainer { HorizontalExpand = true, VerticalExpand = true };
        tabs.AddChild(new ScrollContainer { HScrollEnabled = false, Children = { _bodies } });
        tabs.AddChild(new ScrollContainer { HScrollEnabled = false, Children = { _members } });
        tabs.SetTabTitle(0, Loc.GetString("mind-collective-bodies-tab"));
        tabs.SetTabTitle(1, Loc.GetString("mind-collective-members-tab"));
        content.AddChild(tabs);
        ContentsContainer.AddChild(content);
        _leave.OnPressed += _ => OnLeave?.Invoke();
        OnClose += CloseConfirmation;
        OnClose += CloseControlEditor;
    }

    public void UpdateState(MindCollectiveUiState state)
    {
        Title = state.Name;
        _leave.Visible = !state.InMindspace || state.Bodies.Any(body => body.IsControlling);
        var managementChanged = _lastCanManage != state.CanManage;
        var bodiesAvailableChanged = (_lastBodies.Count > 0) != (state.Bodies.Count > 0);
        if (!_lastBodies.SequenceEqual(state.Bodies) || managementChanged || _lastMindspace != state.InMindspace)
        {
            _lastBodies = state.Bodies.ToList();
            _bodies.RemoveAllChildren();
            if (state.Bodies.Count == 0)
                _bodies.AddChild(Text(Loc.GetString("mind-collective-no-bodies")));
            foreach (var body in state.Bodies)
            {
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new(6) };
                row.AddChild(Text(Loc.GetString("mind-collective-body", ("body", body.Name),
                    ("occupant", body.Occupant ?? Loc.GetString("mind-collective-vacant")))));
                var buttons = new BoxContainer { SeparationOverride = 4, HorizontalExpand = true };
                if (state.InMindspace)
                {
                    var observe = Button(Loc.GetString("mind-collective-observe"));
                    observe.OnPressed += _ => OnObserve?.Invoke(body.Entity);
                    buttons.AddChild(observe);
                }
                var enter = Button(Loc.GetString("mind-collective-enter"));
                enter.Disabled = !body.CanEnter;
                enter.OnPressed += _ => OnEnter?.Invoke(body.Entity);
                buttons.AddChild(enter);
                if (body.CanControl)
                {
                    var control = Button(Loc.GetString(body.IsControlling ? "mind-collective-stop-control" : "mind-collective-control"));
                    control.OnPressed += _ => OnControl?.Invoke(body.Entity);
                    buttons.AddChild(control);
                }
                if (body.CanRecall && body.OccupantMind is { } occupant)
                {
                    var recall = Button(Loc.GetString("mind-collective-recall"));
                    recall.OnPressed += _ => OnRecall?.Invoke(occupant);
                    buttons.AddChild(recall);
                }
                row.AddChild(buttons);
                _bodies.AddChild(new PanelContainer { StyleClasses = { "BorderedWindowPanel" }, Children = { row } });
            }
        }
        if (!SameMembers(_lastMembers, state.Members) || managementChanged || bodiesAvailableChanged)
        {
            _lastMembers = state.Members.ToList();
            _members.RemoveAllChildren();
            foreach (var member in state.Members)
            {
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new(6) };
                row.AddChild(Text(member.Name));
                if (member.CanConfigureControl)
                {
                    var configure = Button(Loc.GetString("body-control-access-title"));
                    configure.Disabled = state.Bodies.Count == 0;
                    configure.OnPressed += _ => OnConfigureControl?.Invoke(member.Mind, null);
                    row.AddChild(configure);
                }
                foreach (var role in member.Roles)
                {
                    if (!member.CanManageRoles)
                    {
                        if (role.Assigned)
                            row.AddChild(Text(role.Name));
                        continue;
                    }
                    var toggle = Button(Loc.GetString(role.Assigned ? "mind-collective-role-revoke" : "mind-collective-role-assign", ("role", role.Name)));
                    toggle.OnPressed += _ => OnSetRole?.Invoke(member.Mind, role.Role, !role.Assigned);
                    row.AddChild(toggle);
                }
                if (member.CanExpel)
                {
                    var expel = Button(Loc.GetString("mind-collective-expel"));
                    expel.OnPressed += _ => OnExpel?.Invoke(member.Mind, 0, false);
                    row.AddChild(expel);
                }
                if (member.CanRecall)
                {
                    var recall = Button(Loc.GetString("mind-collective-recall"));
                    recall.OnPressed += _ => OnRecall?.Invoke(member.Mind);
                    row.AddChild(recall);
                }
                _members.AddChild(new PanelContainer { StyleClasses = { "BorderedWindowPanel" }, Children = { row } });
            }
        }
        _lastCanManage = state.CanManage;
        _lastMindspace = state.InMindspace;
        UpdateControlEditor(state);
        if (state.ExpelTarget is { } target && state.ExpelRequest != 0 && state.ExpelRequest != _expelRequest)
        {
            _expelRequest = state.ExpelRequest;
            var name = state.Members.FirstOrDefault(member => member.Mind == target)?.Name
                ?? Loc.GetString("mind-collective-member");
            ShowExpelConfirmation(target, name, state.ExpelRequest);
        }
        else if (state.ExpelTarget == null)
        {
            _expelRequest = 0;
            CloseConfirmation();
        }
    }

    private void ShowExpelConfirmation(NetEntity target, string name, uint request)
    {
        CloseConfirmation();
        var dialog = new FancyWindow
        {
            Title = Loc.GetString("mind-collective-confirm-title"),
            MinSize = new(320, 160),
            SetSize = Vector2.Max(new(320, 160), Vector2.Min(new(420, 210), UserInterfaceManager.WindowRoot.Size - new Vector2(24))),
        };
        _confirmation = dialog;
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 12, VerticalExpand = true };
        var warning = Text(Loc.GetString("mind-collective-confirm-expel", ("name", name)));
        warning.VerticalExpand = true;
        content.AddChild(warning);
        var buttons = new BoxContainer { SeparationOverride = 8 };
        var yes = Button(Loc.GetString("mind-collective-confirm-yes"));
        var no = Button(Loc.GetString("mind-collective-confirm-no"));
        var accepted = false;
        yes.OnPressed += _ =>
        {
            accepted = true;
            dialog.Close();
            OnExpel?.Invoke(target, request, true);
        };
        no.OnPressed += _ => dialog.Close();
        buttons.AddChild(yes);
        buttons.AddChild(no);
        content.AddChild(buttons);
        dialog.ContentsContainer.AddChild(content);
        dialog.OnClose += () =>
        {
            _confirmation = null;
            if (!accepted)
                OnExpel?.Invoke(target, request, false);
            dialog.Dispose();
        };
        dialog.OpenCentered();
    }

    private void CloseConfirmation() => _confirmation?.Close();

    private void UpdateControlEditor(MindCollectiveUiState state)
    {
        if (state.ControlEditor is not { } editor)
        {
            CloseControlEditor();
            return;
        }
        if (_controlEditorWindow == null)
        {
            _controlEditorWindow = new FancyWindow
            {
                MinSize = new(350, 300),
                SetSize = Vector2.Max(new(350, 300), Vector2.Min(new(440, 540), UserInterfaceManager.WindowRoot.Size - new Vector2(24))),
            };
            _controlEditor = new BodyControlAccessEditor();
            var contents = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
            contents.AddChild(_controlBody);
            contents.AddChild(_controlEditor);
            _controlEditorWindow.ContentsContainer.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { contents } });
            _controlEditor.OnApply += access =>
            {
                if (_editing is { } selected)
                    OnSetControl?.Invoke(selected.Mind, selected.Body, access);
            };
            _controlBody.OnItemSelected += OnControlBodySelected;
            var controlWindow = _controlEditorWindow;
            _controlEditorWindow.OnClose += () =>
            {
                _controlBody.OnItemSelected -= OnControlBodySelected;
                _controlBody.Orphan();
                _controlEditorWindow = null;
                _controlEditor = null;
                if (!_closingControl)
                    OnConfigureControl?.Invoke(null, null);
                controlWindow.Dispose();
            };
            _controlEditorWindow.OpenCentered();
        }
        var selectionChanged = _editing?.Mind != editor.Mind || _editing?.Body != editor.Body;
        if (selectionChanged)
            _controlEditor!.Reset();
        _editing = editor;
        if (selectionChanged || !_controlBodies.Select(body => (body.Entity, body.Name))
                .SequenceEqual(state.Bodies.Select(body => (body.Entity, body.Name))))
        {
            _controlBodies = state.Bodies.ToList();
            _controlBody.Clear();
            for (var i = 0; i < _controlBodies.Count; i++)
            {
                _controlBody.AddItem(_controlBodies[i].Name, i);
                if (_controlBodies[i].Entity == editor.Body)
                    _controlBody.SelectId(i);
            }
        }
        _controlEditorWindow.Title = Loc.GetString("body-control-access-title") + " — " + editor.Name;
        _controlEditor!.UpdateState(editor.Settings);
    }

    private void OnControlBodySelected(OptionButton.ItemSelectedEventArgs args)
    {
        if (_editing is { } selected && args.Id >= 0 && args.Id < _controlBodies.Count)
            OnConfigureControl?.Invoke(selected.Mind, _controlBodies[args.Id].Entity);
    }

    private void CloseControlEditor()
    {
        _closingControl = true;
        _controlEditorWindow?.Close();
        _closingControl = false;
        _editing = null;
    }

    private static bool SameMembers(List<MindCollectiveMemberEntry> previous, List<MindCollectiveMemberEntry> current)
        => previous.Count == current.Count && previous.Zip(current).All(pair => pair.First.Mind == pair.Second.Mind
            && pair.First.Name == pair.Second.Name && pair.First.CanExpel == pair.Second.CanExpel
            && pair.First.CanManageRoles == pair.Second.CanManageRoles && pair.First.CanRecall == pair.Second.CanRecall
            && pair.First.CanConfigureControl == pair.Second.CanConfigureControl
            && pair.First.Roles.SequenceEqual(pair.Second.Roles));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseConfirmation();
            CloseControlEditor();
        }
        base.Dispose(disposing);
    }

    private static Button Button(string text) => new()
    {
        Text = text,
        ToolTip = text,
        ClipText = true,
        HorizontalExpand = true,
        MinHeight = 32,
    };

    private static RichTextLabel Text(string text)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        return label;
    }
}

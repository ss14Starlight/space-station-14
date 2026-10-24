using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.MindCollective;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.MindCollective;

public sealed class MindCollectiveWindow : FancyWindow
{
    public event Action<NetEntity>? OnEnter;
    public event Action<NetEntity>? OnObserve;
    public event Action<NetEntity>? OnRecall;
    public event Action? OnLeave;
    public event Action<NetEntity, uint, bool>? OnExpel;

    private readonly BoxContainer _bodies = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly BoxContainer _members = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly Button _leave = new() { Text = Loc.GetString("mind-collective-leave"), MinHeight = 32 };
    private List<MindCollectiveBodyEntry> _lastBodies = new();
    private List<MindCollectiveMemberEntry> _lastMembers = new();
    private bool? _lastOwner;
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
    }

    public void UpdateState(MindCollectiveUiState state)
    {
        Title = state.Name;
        _leave.Visible = !state.InMindspace;
        var ownerChanged = _lastOwner != state.Owner;
        if (!_lastBodies.SequenceEqual(state.Bodies) || ownerChanged || _lastMindspace != state.InMindspace)
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
                if (state.Owner && body.CanRecall && body.OccupantMind is { } occupant)
                {
                    var recall = Button(Loc.GetString("mind-collective-recall"));
                    recall.OnPressed += _ => OnRecall?.Invoke(occupant);
                    buttons.AddChild(recall);
                }
                row.AddChild(buttons);
                _bodies.AddChild(new PanelContainer { StyleClasses = { "BorderedWindowPanel" }, Children = { row } });
            }
        }
        if (!_lastMembers.SequenceEqual(state.Members) || ownerChanged)
        {
            _lastMembers = state.Members.ToList();
            _members.RemoveAllChildren();
            foreach (var member in state.Members)
            {
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new(6) };
                row.AddChild(Text(member.Name));
                if (state.Owner && member.CanExpel)
                {
                    var expel = Button(Loc.GetString("mind-collective-expel"));
                    expel.OnPressed += _ => OnExpel?.Invoke(member.Mind, 0, false);
                    row.AddChild(expel);
                }
                _members.AddChild(new PanelContainer { StyleClasses = { "BorderedWindowPanel" }, Children = { row } });
            }
        }
        _lastOwner = state.Owner;
        _lastMindspace = state.InMindspace;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            CloseConfirmation();
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

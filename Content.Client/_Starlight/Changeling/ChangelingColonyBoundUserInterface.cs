using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Changeling;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Changeling;

[UsedImplicitly]
public sealed class ChangelingColonyBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ChangelingColonyWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ChangelingColonyWindow>();
        // Mind transfers change the actor; let the server confirm closing this UI.
        _window.OnClose -= Close;
        _window.OnClose += () => SendMessage(new CloseBoundInterfaceMessage());
        _window.OnDetach += part => SendMessage(new ChangelingDetachMessage(part));
        _window.OnSwitch += carrier => SendMessage(new ChangelingSwitchMessage(carrier));
        _window.OnObserve += carrier => SendMessage(new ChangelingObserveMessage(carrier));
        _window.OnLeave += () => SendMessage(new ChangelingLeaveBodyMessage());
        _window.OnRupture += (request, confirmed) => SendMessage(new ChangelingRuptureMessage(request, confirmed));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ChangelingColonyUiState colony)
            _window?.UpdateState(colony);
    }
}

public sealed class ChangelingColonyWindow : FancyWindow
{
    public event Action<NetEntity>? OnDetach;
    public event Action<NetEntity>? OnSwitch;
    public event Action<NetEntity>? OnObserve;
    public event Action? OnLeave;
    public event Action<uint, bool>? OnRupture;

    private readonly BoxContainer _parts = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly BoxContainer _carriers = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly TabContainer _tabs = new() { VerticalExpand = true, HorizontalExpand = true };
    private readonly Button _leave = new() { Text = Loc.GetString("changeling-hive-leave"), MinHeight = 32 };
    private FancyWindow? _confirmation;
    private uint _ruptureRequest;
    private List<ChangelingColonyEntry> _lastParts = new();
    private List<ChangelingColonyEntry> _lastCarriers = new();
    private bool? _lastMindspace;
    private bool? _lastMaster;

    public ChangelingColonyWindow()
    {
        Title = Loc.GetString("changeling-colony-title");
        MinSize = new(350, 320);
        SetSize = Vector2.Max(MinSize, Vector2.Min(new(520, 540), UserInterfaceManager.WindowRoot.Size - new Vector2(24)));
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        content.AddChild(_leave);
        _tabs.AddChild(new ScrollContainer { HScrollEnabled = false, Children = { _carriers } });
        var parts = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        parts.AddChild(Text(Loc.GetString("changeling-colony-parts")));
        parts.AddChild(_parts);
        _tabs.AddChild(new ScrollContainer { HScrollEnabled = false, Children = { parts } });
        _tabs.SetTabTitle(0, Loc.GetString("changeling-colony-bodies-tab"));
        _tabs.SetTabTitle(1, Loc.GetString("changeling-colony-parts-tab"));
        content.AddChild(_tabs);
        ContentsContainer.AddChild(content);
        _leave.OnPressed += _ => OnLeave?.Invoke();
        OnClose += CloseConfirmation;
    }

    public void UpdateState(ChangelingColonyUiState state)
    {
        var masterChanged = _lastMaster != state.Master;
        _leave.Visible = !state.Mindspace;
        if (!state.CanDetach && _tabs.CurrentTab == 1)
            _tabs.CurrentTab = 0;
        _tabs.SetTabVisible(1, state.CanDetach);
        if (!_lastParts.SequenceEqual(state.Parts))
        {
            _lastParts = state.Parts;
            _parts.RemoveAllChildren();
            foreach (var part in state.Parts)
            {
                var button = Button(Loc.GetString("changeling-colony-detach", ("part", part.Name)));
                button.OnPressed += _ => ShowConfirmation(
                    Loc.GetString("changeling-colony-confirm-detach", ("part", part.Name)),
                    () => OnDetach?.Invoke(part.Entity));
                _parts.AddChild(button);
            }
        }
        if (!_lastCarriers.SequenceEqual(state.Carriers) || _lastMindspace != state.Mindspace || masterChanged)
        {
            _lastCarriers = state.Carriers;
            _lastMindspace = state.Mindspace;
            _lastMaster = state.Master;
            _carriers.RemoveAllChildren();
            foreach (var carrier in state.Carriers)
            {
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new(6) };
                row.AddChild(Text(Loc.GetString("changeling-hive-body", ("body", carrier.Name),
                    ("occupant", carrier.Occupant ?? Loc.GetString("changeling-hive-vacant")))));
                var buttons = new BoxContainer { SeparationOverride = 4 };
                if (state.Mindspace)
                {
                    var observe = Button(Loc.GetString("changeling-hive-observe"));
                    observe.OnPressed += _ => OnObserve?.Invoke(carrier.Entity);
                    buttons.AddChild(observe);
                }
                var enter = Button(Loc.GetString("changeling-hive-enter"));
                enter.Disabled = !carrier.CanEnter;
                enter.OnPressed += _ => OnSwitch?.Invoke(carrier.Entity);
                buttons.AddChild(enter);
                row.AddChild(buttons);
                _carriers.AddChild(new PanelContainer { StyleClasses = { "BorderedWindowPanel" }, Children = { row } });
            }
        }
        if (state.ConfirmRupture && state.RuptureRequest != _ruptureRequest)
        {
            _ruptureRequest = state.RuptureRequest;
            ShowConfirmation(Loc.GetString("changeling-colony-confirm-rupture"),
                () => OnRupture?.Invoke(state.RuptureRequest, true), () => OnRupture?.Invoke(state.RuptureRequest, false));
        }
    }

    private void ShowConfirmation(string warning, Action confirmed, Action? cancelled = null)
    {
        CloseConfirmation();
        var dialog = new FancyWindow
        {
            Title = Loc.GetString("changeling-colony-confirm-title"),
            MinSize = new(320, 160),
            SetSize = Vector2.Max(new(320, 160), Vector2.Min(new(420, 210), UserInterfaceManager.WindowRoot.Size - new Vector2(24)))
        };
        _confirmation = dialog;
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 12 };
        var text = Text(warning);
        text.VerticalExpand = true;
        content.AddChild(text);
        var buttons = new BoxContainer { SeparationOverride = 8 };
        var yes = Button(Loc.GetString("changeling-colony-confirm-yes"));
        var no = Button(Loc.GetString("changeling-colony-confirm-no"));
        var accepted = false;
        yes.OnPressed += _ =>
        {
            accepted = true;
            dialog.Close();
            confirmed();
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
                cancelled?.Invoke();
            dialog.Dispose();
        };
        dialog.OpenCentered();
    }

    private void CloseConfirmation()
    {
        _confirmation?.Close();
    }

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
        MinHeight = 32
    };

    private static RichTextLabel Text(string text)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        return label;
    }
}

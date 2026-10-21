using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Changeling;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Starlight.Changeling;

[UsedImplicitly]
public sealed class ChangelingColonyBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ChangelingColonyWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ChangelingColonyWindow>();
        _window.OnDetach += part => SendMessage(new ChangelingDetachMessage(part));
        _window.OnSwitch += carrier => SendMessage(new ChangelingSwitchMessage(carrier));
        _window.OnAnimate += enabled => SendMessage(new ChangelingAnimateBloodMessage(enabled));
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
    public event Action<bool>? OnAnimate;

    private readonly BoxContainer _parts = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _carriers = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Button _animate = new();
    private bool _animating;
    private List<ChangelingColonyEntry> _lastParts = new();
    private List<ChangelingColonyEntry> _lastCarriers = new();

    public ChangelingColonyWindow()
    {
        Title = Loc.GetString("changeling-colony-title");
        MinSize = new(370, 300);
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        content.AddChild(_animate);
        content.AddChild(new Label { Text = Loc.GetString("changeling-colony-parts") });
        content.AddChild(_parts);
        content.AddChild(new Label { Text = Loc.GetString("changeling-colony-carriers") });
        content.AddChild(_carriers);
        ContentsContainer.AddChild(new ScrollContainer { Children = { content } });
        _animate.OnPressed += _ => OnAnimate?.Invoke(!_animating);
    }

    public void UpdateState(ChangelingColonyUiState state)
    {
        _animating = state.AnimateBlood;
        _animate.Text = Loc.GetString(_animating ? "changeling-colony-animation-on" : "changeling-colony-animation-off");
        if (!_lastParts.SequenceEqual(state.Parts))
        {
            _lastParts = state.Parts;
            Fill(_parts, state.Parts, "changeling-colony-detach", part => OnDetach?.Invoke(part));
        }
        if (!_lastCarriers.SequenceEqual(state.Carriers))
        {
            _lastCarriers = state.Carriers;
            Fill(_carriers, state.Carriers, "changeling-colony-switch", carrier => OnSwitch?.Invoke(carrier));
        }
    }

    private static void Fill(BoxContainer container, List<ChangelingColonyEntry> entries, string label, Action<NetEntity> pressed)
    {
        container.RemoveAllChildren();
        foreach (var entry in entries)
        {
            var button = new Button { Text = Loc.GetString(label, ("part", entry.Name)) };
            button.OnPressed += _ => pressed(entry.Entity);
            container.AddChild(button);
        }
    }
}

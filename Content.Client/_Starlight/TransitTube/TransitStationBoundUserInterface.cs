using Content.Shared._Starlight.TransitTube;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Starlight.TransitTube;

/// <summary>
/// Destination picker of a transit tube station: a list of stations the docked pod can reach,
/// plus "any free station" and "no destination".
/// </summary>
public sealed partial class TransitStationBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private BoxContainer? _list;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("transit-station-ui-title");
        _window.MinSize = new(300, 200);

        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        _window.Contents.AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            Children = { _list },
        });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not TransitStationBoundUserInterfaceState cast || _list == null)
            return;

        _list.RemoveAllChildren();

        AddButton(Loc.GetString("transit-station-ui-none"), cast.Selected == null && !cast.AnyFree, null, false);
        AddButton(Loc.GetString("transit-station-ui-any"), cast.AnyFree, null, true);

        if (cast.Destinations.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("transit-station-ui-empty") });
            return;
        }

        foreach (var entry in cast.Destinations)
        {
            AddButton(entry.Name, cast.Selected == entry.Station, entry.Station, false);
        }
    }

    private void AddButton(string text, bool selected, NetEntity? destination, bool anyFree)
    {
        // Pressed state is set by hand: grouped buttons misbehave in reopened windows.
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            Pressed = selected,
            HorizontalExpand = true,
        };

        button.OnPressed += _ => SendMessage(new TransitStationSelectDestinationMessage(destination, anyFree));
        _list?.AddChild(button);
    }
}

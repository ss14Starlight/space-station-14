using Content.Shared.Pinpointer;
using Robust.Client.UserInterface;

namespace Content.Client.Pinpointer.UI;

public sealed class StationMapBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private StationMapWindow? _window;

    public StationMapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        // Starlight - start
        EntityUid? trackedEntity = null;

        if (EntMan.TryGetComponent<StationMapComponent>(Owner, out var comp))
        {
            if (comp.ShowLocation)
                trackedEntity = Owner;
        }
        // Starlight - end

        _window = this.CreateWindow<StationMapWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _window.Set(trackedEntity, Owner); // Starlight
    }
}

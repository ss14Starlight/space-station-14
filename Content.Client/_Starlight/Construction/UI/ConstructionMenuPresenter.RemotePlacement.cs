using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared.Construction.Prototypes;

namespace Content.Client.Construction.UI;

internal sealed partial class ConstructionMenuPresenter
{
    private bool TryBeginRemotePlacement(ConstructionPrototype prototype)
        => _entManager.System<RemoteConstructionPlacementSystem>().TryBegin(prototype);

    private bool StopRemotePlacement()
    {
        var placement = _entManager.System<RemoteConstructionPlacementSystem>();
        if (!placement.IsActive)
            return false;

        placement.Clear();
        return true;
    }
}

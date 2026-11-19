using Content.Client._Starlight.Computers.RemoteControl;
using Robust.Client.Placement;
using Robust.Shared.Enums;

namespace Content.Client.Construction;

public sealed partial class ConstructionSystem
{
    #region Starlight
    [Dependency] private IPlacementManager _quickConstructionPlacementManager = null!;

    private bool TryFlipQuickConstructionPlacement()
    {
        var remotePlacementSystem = EntityManager.System<RemoteConstructionPlacementSystem>();
        if (remotePlacementSystem.IsActive)
        {
            remotePlacementSystem.TryFlip();
            return true;
        }

        if (!_quickConstructionPlacementManager.IsActive || _quickConstructionPlacementManager.Eraser)
            return false;

        if (_quickConstructionPlacementManager is not PlacementManager
            {
                Hijack: ConstructionPlacementHijack { IsQuickConstruction: true } hijack
            })
            return false;

        if (hijack.CurrentPrototype?.Mirror is not { } mirror)
            return true;

        var prototype = PrototypeManager.Index(mirror);
        _quickConstructionPlacementManager.BeginPlacing(
            new PlacementInformation
            {
                IsTile = false,
                PlacementOption = prototype.PlacementMode
            },
            new ConstructionPlacementHijack(this, prototype, isQuickConstruction: true));

        return true;
    }
    #endregion
}

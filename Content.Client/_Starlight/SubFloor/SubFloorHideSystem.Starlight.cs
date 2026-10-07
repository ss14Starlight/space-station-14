using Content.Shared.SubFloor;

// ReSharper disable once CheckNamespace
namespace Content.Client.SubFloor;

public sealed partial class SubFloorHideSystem
{

    public void ToggleLayer(SubFloorVisibilityMask mask)
    {
        if (mask != SubFloorVisibilityMask.None) _showLayers ^= mask;
        else _showLayers = SubFloorVisibilityMask.None;

        var ev = new ShowSubfloorRequestEvent
        {
            Value = _showLayers != SubFloorVisibilityMask.None,
        };
        RaiseNetworkEvent(ev);
    }

    public void SetLayer(SubFloorVisibilityMask mask, bool visible)
    {
        if (visible)
            _showLayers = mask;
        else
            _showLayers = ~mask;

        var ev = new ShowSubfloorRequestEvent
        {
            Value = visible,
        };
        RaiseNetworkEvent(ev);
    }
}

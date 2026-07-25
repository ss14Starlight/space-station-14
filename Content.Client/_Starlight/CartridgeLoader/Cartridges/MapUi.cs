using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.CartridgeLoader.Cartridges;

public sealed partial class MapUi : UIFragment
{
    private MapUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface ui, EntityUid? fragmentOwner)
    {
        _fragment = new MapUiFragment();

    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if(state is MapUiState mapState) _fragment?.UpdateState(mapState);
    }
}

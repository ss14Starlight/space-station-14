using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared._Daiquiri.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client._Daiquiri.CartridgeLoader.Cartridges;

public sealed partial class GoogolplexUi : UIFragment
{
    private GoogolplexUiFragment? _fragment;
    private BoundUserInterface? _boundUi;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _boundUi = userInterface;
        _fragment = new GoogolplexUiFragment();
        _fragment.SetBoundUi(userInterface);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (_fragment == null)
            return;

        switch (state)
        {
            case StarWayUiState starWayState:
                _fragment.OnStarWayStateReceived(starWayState);
                break;
            case StarWayThreadIndexState indexState:
                _fragment.OnThreadIndexReceived(indexState);
                break;
            case ChessLinkStatusState chessStatusState:
                _fragment.OnChessLinkStatusReceived(chessStatusState);
                break;
            case ChessLinkStartState chessStartState:
                _fragment.OnChessLinkStartReceived(chessStartState);
                break;
            case ChessGameState chessGameState:
                _fragment.OnChessGameStateReceived(chessGameState);
                break;
        }
    }

    /// <summary>
    /// Called by the fragment to send a cartridge message to the server.
    /// </summary>
    public void SendCartridgeMessage(CartridgeMessageEvent message)
    {
        if (_boundUi != null)
        {
            _boundUi.SendMessage(new CartridgeUiMessage(message));
        }
    }
}

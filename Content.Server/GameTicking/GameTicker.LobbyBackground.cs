using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    // STARLIGHT: Support for conditional lobby backgrounds
    private ProtoId<LobbyBackgroundPrototype>? _forcedLobbyBackground; //starlight, art credit system
    public ProtoId<LobbyBackgroundPrototype>? LobbyBackground { get; private set; }

    [ViewVariables]
    private List<ProtoId<LobbyBackgroundPrototype>>? _lobbyBackgrounds;

    private static readonly string[] WhitelistedBackgroundExtensions = new string[] {"png", "jpg", "jpeg", "webp"};

    private void InitializeLobbyBackground()
    {
        var allprotos = _prototypeManager.EnumeratePrototypes<LobbyBackgroundPrototype>().ToList();
        _lobbyBackgrounds ??= new List<ProtoId<LobbyBackgroundPrototype>>();

        //create protoids from them
        foreach (var proto in allprotos)
        {
            var ext = proto.Background.Extension;
            if (!WhitelistedBackgroundExtensions.Contains(ext))
                continue;

            //create a protoid and add it to the list
            _lobbyBackgrounds.Add(new ProtoId<LobbyBackgroundPrototype>(proto.ID));
        }

        RandomizeLobbyBackground();
    }

    private void RandomizeLobbyBackground() {
        // STARLIGHT: Check if we have a forced background first
        if (_forcedLobbyBackground != null)
        {
            LobbyBackground = _forcedLobbyBackground;
            _forcedLobbyBackground = null; // Reset after use
            return;
        }

        if (_lobbyBackgrounds != null && _lobbyBackgrounds.Count != 0)
            LobbyBackground = _robustRandom.Pick(_lobbyBackgrounds);
        else
            LobbyBackground = null;
    }

    /// <summary>
    /// STARLIGHT: Sets a specific lobby background to be used on the next round restart.
    /// </summary>
    /// <param name="lobbyProto">The path to the background image</param>
    public void SetLobbyBackground(ProtoId<LobbyBackgroundPrototype> lobbyProto) //starlight
    {
        _forcedLobbyBackground = lobbyProto; //starlight
    }
}

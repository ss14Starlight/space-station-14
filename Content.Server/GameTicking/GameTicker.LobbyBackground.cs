using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [ViewVariables]
    public ProtoId<LobbyBackgroundPrototype>? LobbyBackground { get; set; } //starlight, art credit system

    [ViewVariables]
    private List<ProtoId<LobbyBackgroundPrototype>>? _lobbyBackgrounds; //starlight, art credit system

    // STARLIGHT: Support for conditional lobby backgrounds
    private ProtoId<LobbyBackgroundPrototype>? _forcedLobbyBackground; //starlight, art credit system

    private static readonly string[] WhitelistedBackgroundExtensions = new string[] {"png", "jpg", "jpeg", "webp"};

    private void InitializeLobbyBackground()
    {
        //starlight start, art credit system
        var allprotos = _prototypeManager.EnumeratePrototypes<LobbyBackgroundPrototype>().ToList();
        //create protoids from them
        foreach (var proto in allprotos)
        {
            var ext = proto.Background.Extension;
            if (WhitelistedBackgroundExtensions.Contains(ext))
            {
                //filter out ones with exclude from menu
                if (proto.ExcludeFromMenu)
                    continue;
                //create a protoid and add it to the list
                _lobbyBackgrounds ??= new List<ProtoId<LobbyBackgroundPrototype>>();
                _lobbyBackgrounds.Add(new ProtoId<LobbyBackgroundPrototype>(proto.ID));
            }
        }
        //starlight end, art credit system

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

        //starlight start, art credit system
        if (_lobbyBackgrounds!.Any())
        {
            LobbyBackground = _robustRandom.Pick(_lobbyBackgrounds!);
        }
        else
        {
            LobbyBackground = null;
        }
        //starlight end, art credit system
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

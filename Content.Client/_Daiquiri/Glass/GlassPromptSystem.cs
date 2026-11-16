using System;
using System.Linq;
using Content.Client.Audio;
using Content.Client._Daiquiri.Options.UI;
using Content.Shared._Starlight.Glass;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Client._Daiquiri.Glass;

public sealed partial class GlassPromptSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private ContentAudioSystem _audio = default!;

    private bool _newcomerTrackPending;

    public override void Initialize()
    {
        base.Initialize();
        _net.RegisterNetMessage<GlassPromptShowMessage>();
        SubscribeNetworkEvent<GlassPromptShowEvent>(OnShowPromptEvent);
        _audio.LobbySoundtrackChanged += OnLobbySoundtrack;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _audio.LobbySoundtrackChanged -= OnLobbySoundtrack;
    }

    private void OnShowPromptEvent(GlassPromptShowEvent ev)
    {
        Logger.InfoS("glassprompt", "Received GlassPromptShowEvent, opening the newcomer overlay.");
        GlassThemePrompt.Open(_ui);

        // Daiquiri
        _newcomerTrackPending = true;
        TryNewcomerTrack();
    }

    private void OnShowPrompt(GlassPromptShowMessage message)
    {
        Logger.InfoS("glassprompt", "Received GlassPromptShowMessage, opening the newcomer overlay.");
        GlassThemePrompt.Open(_ui);
    }

    private void OnLobbySoundtrack(LobbySoundtrackChangedEvent ev)
    {
        // Playlist may arrive after the prompt - retry the track switch once.
        if (!_newcomerTrackPending)
            return;
        _newcomerTrackPending = false;
        TryNewcomerTrack();
    }

    private void TryNewcomerTrack()
    {
        var track = _audio.LobbyPlaylistTracks
            ?.FirstOrDefault(f => f.Contains("timefracture", StringComparison.OrdinalIgnoreCase));
        if (track == null || _audio.CurrentLobbyTrack == track)
            return;

        _audio.PlayLobbyTrack(track);
    }
}

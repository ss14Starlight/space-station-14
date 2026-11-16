using Robust.Shared.Serialization;

namespace Content.Shared.Audio.Events;

/// <summary>
/// Event of changing lobby music playlist (on server).
/// </summary>
[Serializable, NetSerializable]
public sealed class LobbyPlaylistChangedEvent : EntityEventArgs
{
    /// <inheritdoc />
    public LobbyPlaylistChangedEvent(string[] playlist, bool postRound = false) // Starlight
    {
        Playlist = playlist;
        PostRound = postRound; // Starlight
    }

    /// <summary>
    /// List of soundtrack filenames for lobby playlist.
    /// </summary>
    public string[] Playlist;

    #region Starlight

    /// <summary>
    /// <see langword="true"/> if game is in post-round run level.
    /// </summary>
    public bool PostRound;

    #endregion
}

/// <summary>
/// Event of stopping lobby music.
/// </summary>
[Serializable, NetSerializable]
public sealed class LobbyMusicStopEvent : EntityEventArgs
{
}

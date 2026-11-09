using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Replay;

/// <summary>
/// Who a private sound was played to, for replay viewers. Replays record every audio entity regardless of its filter,
/// so without this, sounds meant for one player play for everyone. Session-specific and refused to every real player,
/// so only replays receive it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReplayAudioAudienceComponent : Component
{
    public override bool SessionSpecific => true;

    [AutoNetworkedField]
    public HashSet<EntityUid> Audience = new();

    /// <summary>
    /// Client-side: the volume to restore once the sound is unmuted, or null while it isn't muted.
    /// </summary>
    [ViewVariables]
    public float? MutedVolume;
}

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VinylPlayerComponent : Component
{
    /// <summary>
    /// Plays out loud around the player instead of over the station radio, no rig or server needed.
    /// </summary>
    [DataField]
    public bool Standalone;

    /// <summary>
    /// Containers holding the discs, played in this order.
    /// </summary>
    [DataField]
    public string[] Slots = ["vinyl"];

    /// <summary>
    /// Start playing as soon as a disc goes in. Decks that cue a disc for later leave this off.
    /// </summary>
    [DataField]
    public bool AutoPlay = true;

    /// <summary>
    /// Start the next disc when one ends. Decks for adverts leave this off so the host picks what comes next.
    /// </summary>
    [DataField]
    public bool AutoAdvance = true;

    /// <summary>
    /// Go back to the first slot after the last one instead of stopping.
    /// </summary>
    [DataField]
    public bool Loop;

    /// <summary>
    /// Activating the player starts or pauses it. Off for machines whose activation opens a UI.
    /// </summary>
    [DataField]
    public bool ToggleOnActivate = true;

    /// <summary>
    /// Name shown on this player's mixer strip instead of the entity name.
    /// </summary>
    [DataField]
    public LocId? ChannelName;

    /// <summary>
    /// Index in <see cref="Slots"/> of the disc being played or cued.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int CurrentSlot;

    [ViewVariables, AutoNetworkedField]
    public bool Playing;

    /// <summary>
    /// Where the current disc is, while paused.
    /// </summary>
    [ViewVariables]
    public TimeSpan Position;

    /// <summary>
    /// When the current disc would have started from its beginning, while playing.
    /// </summary>
    [ViewVariables]
    public TimeSpan PlaybackStart;

    /// <summary>
    /// When the current disc runs out, while playing.
    /// </summary>
    [ViewVariables]
    public TimeSpan PlaybackEnd;

    /// <summary>
    /// The sound entity being played by a standalone player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SoundEntity;

    /// <summary>
    /// Default audio params for the played audio.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AudioParams DefaultParams = AudioParams.Default.WithVolume(3.5f).WithMaxDistance(8f); // 8 is just the edge of the screen usually

    /// <summary>
    /// Sprite state of the display layer while a disc is loaded.
    /// </summary>
    [DataField]
    public string? DisplayState;

    /// <summary>
    /// Sprite state of the display layer while the player is empty.
    /// </summary>
    [DataField]
    public string? DisplayEmptyState;
}

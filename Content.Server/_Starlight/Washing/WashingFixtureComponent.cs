using Robust.Shared.Audio;

namespace Content.Server._Starlight.Washing;

/// <summary>
/// This component indicates that an object can be used for washing.
/// </summary>
[RegisterComponent]
public sealed partial class WashingFixtureComponent : Component
{
    /// <summary>
    /// How long it takes to wash using this entity
    /// </summary>
    [DataField]
    public float CleanDelay = 3.0f;

    /// <summary>
    /// How far away it is possible to wash using this entity
    /// </summary>
    [DataField]
    public float CleanDistance = 1.5f;

    /// <summary>
    /// The sound to play on starting washing.
    /// </summary>
    [DataField]
    public SoundSpecifier SoundStart = new SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg")
    {
        Params = AudioParams.Default.WithVolume(-6f).WithMaxDistance(4),
    };

    /// <summary>
    /// The sound to play on completing washing.
    /// </summary>
    [DataField]
    public SoundSpecifier SoundCompleted = new SoundPathSpecifier("/Audio/Ambience/Objects/drain.ogg")
    {
        Params = AudioParams.Default.WithVolume(-6f).WithMaxDistance(4),
    };
}

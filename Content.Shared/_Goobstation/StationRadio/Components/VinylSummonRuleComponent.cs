using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes; // Starlight - Dehardcode Audio Params and Ash Prototype

namespace Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation

/// <summary>
/// Component that allows a vinyl disk to spawn a game rule when it finishes playing.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VinylSummonRuleComponent : Component
{
    /// <summary>
    /// The game rule prototype to spawn when the vinyl finishes playing.
    /// </summary>
    [DataField(required: true)]
    public string GameRule = string.Empty;

    /// <summary>
    /// Sound played when the vinyl burns to ash.
    /// </summary>
    [DataField]
    public SoundSpecifier BurnSound = new SoundPathSpecifier("/Audio/Effects/cig_snuff.ogg");

    // Starlight - Dehardcode Audio Params and Ash Prototype
    /// <summary>
    /// Set default volume for the Burn sound.
    /// </summary>
    [DataField]
    public AudioParams BurnSoundParams = AudioParams.Default.WithVolume(-5f);

    /// <summary>
    /// The prototype that is spawned when the vinyl burns to ash.
    /// </summary>
    [DataField]
    public EntProtoId AshPrototype = "Ash";
    // Starlight - End
}

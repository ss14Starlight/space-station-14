using Robust.Shared.Audio; // Starlight - Dehardcode Audio Params
using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.StationRadio.Components; // Starlight - _Goob -> _Goobstation

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VinylPlayerComponent : Component
{
    /// <summary>
    /// Should the vinyl player relay to radios around the station, should only be true for the radiostation vinyl player
    /// </summary>
    [DataField]
    public bool RelayToRadios;

    /// <summary>
    /// The sound entity being played
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SoundEntity;

    // Starlight - Dehardcode Audo Params
    /// <summary>
    /// Default audio params for the played audio.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AudioParams DefaultParams = AudioParams.Default.WithVolume(3.5f).WithMaxDistance(8f); // 8 is just the edge of the screen usually
    // Starlight - End
}

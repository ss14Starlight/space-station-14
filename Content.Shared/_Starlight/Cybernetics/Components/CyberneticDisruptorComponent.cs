using Robust.Shared.Audio;

namespace Content.Shared._Starlight.Cybernetics.Components;

/// <summary>
/// For tools which can be used to temporarily disable active cyberware.
/// </summary>
[RegisterComponent]
public sealed partial class CyberneticDisruptorComponent : Component
{
    [DataField]
    public TimeSpan UseTime = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Should the duration be replaced (true) or added (false)
    /// </summary>
    [DataField]
    public bool RefreshDuration = true;

    /// <summary>
    /// The sound to make when we start disrupting something.
    /// </summary>
    [DataField]
    public SoundSpecifier SoundStart = new SoundPathSpecifier("/Audio/Machines/energyshield_ambient.ogg", AudioParams.Default.WithVolume(0.1f));

    /// <summary>
    /// The sound to make when we finish disrupting something
    /// </summary>
    [DataField]
    public SoundSpecifier SoundFinish = new SoundPathSpecifier("/Audio/Machines/anomaly_sync_connect.ogg", AudioParams.Default.WithVolume(0.1f));
}

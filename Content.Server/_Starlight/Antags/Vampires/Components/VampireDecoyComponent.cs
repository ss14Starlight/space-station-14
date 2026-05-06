using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Antags.Vampires.Components;

/// <summary>
/// Handles the simple runtime behaviour for vampire decoy entities.
/// </summary>
[RegisterComponent]
public sealed partial class VampireDecoyComponent : Component
{
    /// <summary>
    /// Ensures the flash/explosion only happens once.
    /// </summary>
    public bool Detonated;

    [DataField]
    public EntProtoId FlashEffectId = "GrenadeFlashEffect";

    [DataField]
    public float FlashRange = 3f;

    [DataField]
    public TimeSpan FlashDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public float SlowTo = 0.5f;

    [DataField]
    public bool DisplayPopup = true;

    [DataField]
    public float Probability = 1f;

    [DataField]
    public SoundSpecifier FlashSound = new SoundPathSpecifier("/Audio/Weapons/flash.ogg");
}

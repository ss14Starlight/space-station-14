// SPDX-FileCopyrightText: 2025 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared._Funkystation.Traits.Assorted;

/// <summary>
/// Visual states for the seizure overlay.
/// </summary>
public enum SeizureVisualState : byte
{
    Prodrome = 0,  // Faint warning overlay
    Seizure = 1    // Harsh seizure overlay
}

/// <summary>
/// Component for seizure visual overlay effects that mimics MigraineComponent structure
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeizureOverlayComponent : Component
{
    /// <summary>
    /// The target magnitude of vision impairment during the seizure (0-6).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float BlurryMagnitude = 1.5f;

    /// <summary>
    /// Current interpolated blur value (smoothly transitions to target).
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public float CurrentBlur = 0f;

    /// <summary>
    /// Time accumulated for pulsing effect.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public float PulseAccumulator = 0f;

    /// <summary>
    /// How fast the blur ramps up (units per second).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float RampUpSpeed = 2f;

    /// <summary>
    /// How fast the blur ramps down (units per second).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("rampDownSpeed"), AutoNetworkedField]
    public float RampDownSpeed = 2f;

    /// <summary>
    /// Frequency of the pulsing effect (cycles per second).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float PulseFrequency = 0.8f;

    /// <summary>
    /// Amplitude of the pulsing effect (how much it varies from base).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float PulseAmplitude = 0.3f;

    /// <summary>
    /// Whether to use soft shader effects.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("useSoftShader"), AutoNetworkedField]
    public bool UseSoftShader = false;

    /// <summary>
    /// Multiplier applied to shader strength when UseSoftShader is true.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("softness"), AutoNetworkedField]
    public float Softness = 0.45f;

    /// <summary>
    /// Whether the overlay is fading out.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("isFading"), AutoNetworkedField]
    public bool IsFading = false;

    /// <summary>
    /// How long the fadeout should take when the seizure ends (in seconds).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float FadeOutDuration = 1f;

    /// <summary>
    /// Current state of the seizure visual effects.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public SeizureVisualState VisualState = SeizureVisualState.Prodrome;

    /// <summary>
    /// Current movement speed multiplier for gradual recovery.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float MovementSpeedMultiplier = 1.0f;

    /// <summary>
    /// Whether this overlay should handle movement speed recovery during fade.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public bool HandleMovementRecovery = false;
}


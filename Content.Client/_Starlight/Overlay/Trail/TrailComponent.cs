using System.Numerics;
using Content.Client._Starlight.Collections;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Trail;

namespace Content.Client._Starlight.Trail;

/// <summary>
/// Marks an entity to leave a fading motion trail behind it.
/// </summary>
[RegisterComponent]
public sealed partial class TrailComponent : Component
{
    /// <summary>If there is a shader, there is not much point in this.</summary>
    [DataField]
    public Color TrailColor = Color.White;

    /// <summary>
    /// Color to fade to as trail points age. This allows for a gradient effect along the trail, or a fade to a specific color before disappearing. If fully transparent, points will simply fade out without changing hue. If set to a color with alpha, points will blend towards that color as they decay.
    /// </summary>
    [DataField]
    public Color FadeColor = Color.Transparent;

    /// <summary>Maximum number of recorded positions.</summary>
    [DataField]
    public int MaxPoints = 32;

    /// <summary>Minimum distance between recorded points.</summary>
    [DataField]
    public float MinDistance = 0.24f;

    /// <summary>Width of the trail strip at the head.</summary>
    [DataField]
    public float LineWidth = 0.65f;

    /// <summary>Optional shader prototype applied to the trail geometry.</summary>
    [DataField]
    public ProtoId<ShaderPrototype>? Shader;

    /// <summary>Seconds to wait before the trail starts fading when idle.</summary>
    [DataField]
    public float DecayDelay = 0.2f;

    /// <summary>Seconds between each point removal during decay.</summary>
    [DataField]
    public float DecayInterval = 0.03f;

    /// <summary>Recorded world positions forming the trail.</summary>
    public RingBuffer<Vector2> Points = new(32);

    /// <summary>Recorded world positions and params forming the trail.</summary>
    public RingBuffer<TrailSample> Samples = new(32);

    /// <summary>Time spent idle (not moving) for decay logic.</summary>
    public float IdleTimer;

    /// <summary>
    /// Determines how the trail is rendered. "Ribbon" mode creates a simple triangle strip between points, while "SpriteGhost" mode spawns a fading ghost sprite at each point.
    /// </summary>
    [DataField]
    public TrailMode Mode = TrailMode.Ribbon;

    /// <summary>
    /// Determines how may of samples we should skip between each render. I.e. means if skip is 1, we will render every other sample, if skip is 2, we will render every third sample and so on. This can be used to create a more sparse trail effect without needing to reduce the MaxPoints or increase the MinDistance.
    /// </summary>
    [DataField]
    public int SkipSamples = 0;

    [DataField]
    public float TeleportThreshold = 3f;
}

public struct TrailSample
{
    public Vector2 Position;
    public Angle Rotation;
    public Angle EyeRotation;
}

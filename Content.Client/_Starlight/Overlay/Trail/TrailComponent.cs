using System.Numerics;
using Content.Client._Starlight.Collections;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

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

    /// <summary>Time spent idle (not moving) for decay logic.</summary>
    public float IdleTimer;
}

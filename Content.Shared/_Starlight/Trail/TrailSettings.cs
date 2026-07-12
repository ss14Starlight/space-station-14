namespace Content.Shared._Starlight.Trail;

/// <summary>
/// Data definition for configuring a motion trail on any entity.
/// </summary>
[DataDefinition]
public sealed partial class TrailSettings
{
    [DataField]
    public Color Color = Color.White;

    [DataField]
    public Color FadeColor = Color.Transparent;

    [DataField]
    public int MaxPoints = 64;

    [DataField]
    public float LineWidth = 0.65f;

    [DataField]
    public float MinDistance = 0.12f;

    /// <summary>Seconds before idle trail starts decaying.</summary>
    [DataField]
    public float DecayDelay = 0.2f;

    /// <summary>Seconds between each point removal during decay.</summary>
    [DataField]
    public float DecayInterval = 0.03f;

    /// <summary>Optional shader prototype ID applied to the trail geometry.</summary>
    [DataField]
    public string? Shader;

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
}

public enum TrailMode
{
    Ribbon, // A simple triangle strip.
    SpriteGhost // A ghostly sprite at each point, fading out over time.
}

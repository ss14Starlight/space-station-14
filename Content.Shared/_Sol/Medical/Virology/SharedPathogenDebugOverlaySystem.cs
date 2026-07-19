namespace Content.Shared._Sol.Medical.Virology;

/// <summary>
/// Shared constants for the admin pathogen-air debug overlay.
/// </summary>
public abstract class SharedPathogenDebugOverlaySystem : EntitySystem
{
    public const int LocalViewRange = 16;
    protected float AccumulatedFrameTime;
}

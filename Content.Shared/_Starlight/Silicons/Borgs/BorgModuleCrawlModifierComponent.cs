using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// When attached to a cyborg chassis, modifies crawling speed when a module is active.
/// Decouples crawl speed from hand count and applies a fixed multiplier instead.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BorgModuleCrawlModifierComponent : Component
{
    /// <summary>
    /// Speed multiplier applied while knocked down and a cyborg module is selected.
    /// </summary>
    [DataField]
    public float ActiveSpeedModifier = 0.5f;
}

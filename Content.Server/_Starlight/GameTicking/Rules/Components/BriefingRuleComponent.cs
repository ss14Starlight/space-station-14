using Robust.Shared.Audio;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

/// <summary>
/// Sends a configurable briefing to antags selected by this game rule.
/// </summary>
[RegisterComponent]
public sealed partial class BriefingRuleComponent : Component
{
    /// <summary>
    /// Loc string used for the briefing.
    /// Supports {$direction}.
    /// </summary>
    [DataField(required: true)]
    public string Briefing = string.Empty;

    /// <summary>
    /// Optional briefing chat color.
    /// </summary>
    [DataField]
    public Color? Color;

    /// <summary>
    /// Optional briefing sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;
}

using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Paper;

/// <summary>
/// This component indicates that the piece of paper is supposed to have regex-parsable content,
/// and provides the fields to detect that content.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ParsablePaperComponent : Component
{
    /// <summary>
    /// List of regex patterns that MUST be matched in order for the paper to be "valid"
    /// </summary>
    [DataField]
    public List<string> RequiredPatterns = new();

    /// <summary>
    /// Dictionary of regex patterns returning values, and their associated names.
    /// Used to parse information from a piece of paper.
    /// These patterns to not indicate paper validity, i.e. being unable to find
    /// one of these values still makes the paper "valid" according to RequiredPatterns.
    /// </summary>
    [DataField]
    public Dictionary<string, string> RequestedValuePatterns = new();
}

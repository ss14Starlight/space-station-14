using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Paper;

/// <summary>
/// This component indicates that an entity should use a different font for signatures.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SignatureFontComponent : Component
{
    /// <summary>
    /// The path to the font signatures by this entity should use.
    /// </summary>
    [DataField]
    public string FontPath = "/Fonts/_Starlight/Signature.ttf";
}
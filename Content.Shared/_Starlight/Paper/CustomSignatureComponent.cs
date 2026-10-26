using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Paper;

/// <summary>
/// This component indicates that an entity should use a different font for signatures.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CustomSignatureComponent : Component
{
    [DataField] public SignatureConfig Config = new();
}

[Serializable, NetSerializable, DataRecord]
public partial record struct SignatureConfig(
    [property:ViewVariables(VVAccess.ReadWrite)] string? FontPath = null,
    [property:ViewVariables(VVAccess.ReadWrite)] Color? PenColor = null
)
{
    public SignatureConfig() : this("/Fonts/_Starlight/Signature.ttf")
    { }
    public SignatureConfig(string? fontPath) : this(fontPath, Color.FromHex("#333333"))
    { }

    public SignatureConfig Saturate(SignatureConfig other) => new(other.FontPath ?? FontPath, other.PenColor ?? PenColor);
}

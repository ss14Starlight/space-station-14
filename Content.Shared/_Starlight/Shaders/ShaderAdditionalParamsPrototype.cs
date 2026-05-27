using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Shaders;

/// <summary>
/// Additional parameters (textures) to bind to a shader prototype.
/// ID must match the shader prototype ID it extends.
/// </summary>
[Prototype]
public sealed partial class ShaderAdditionalParamsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Map of uniform name -> texture resource path.
    /// These textures are automatically bound when the shader instance is created.
    /// </summary>
    [DataField]
    public Dictionary<string, ResPath> Textures = [];
}

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Sprite;

/// <summary>
/// Overrides the color/shader of layers on the attached entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ColorControlComponent : Component
{
    /// <summary>
    /// Color to apply.
    /// </summary>
    [DataField, AutoNetworkedField] public Color? Color;

    /// <summary>
    /// How <see cref="Color"/> is applied to each layer.
    /// </summary>
    [DataField, AutoNetworkedField] public ColorControlMode Mode = ColorControlMode.Replace;

    /// <summary>
    /// Shader prototype to apply, e.g. "unshaded" or "shaded".
    /// Attempts to use displacement variants if they exist to keep clothing working etc.
    /// </summary>
    [DataField, AutoNetworkedField] public string? Shader;

    /// <summary>
    /// Which groups of layers to affect. Ignored if <see cref="Layers"/> or <see cref="LayerIndices"/> is non-empty.
    /// </summary>
    [DataField, AutoNetworkedField] public ColorControlTargets Targets = ColorControlTargets.Body;

    /// <summary>
    /// Manually picked layers, by layer key.
    /// </summary>
    [DataField, AutoNetworkedField] public List<string> Layers = [];

    /// <summary>
    /// Manually picked layers, by index.
    /// </summary>
    /// <remarks>
    /// Please don't use this but as last resort, things really should have keys.
    /// </remarks>
    [DataField, AutoNetworkedField] public List<int> LayerIndices = [];

    /// <summary>
    /// Per-layer settings by layer key.
    /// </summary>
    [DataField, AutoNetworkedField] public Dictionary<string, ColorControlLayerOverride> LayerOverrides = new();

    // ReSharper disable once UseCollectionExpression | Client sandboxing skill issue
    [DataField, AutoNetworkedField] public HashSet<string> IgnoredShaders = new()
    {
        "StencilMask",
        "StencilDraw",
        "StencilClear",
        "StencilEqualDraw",
        "DisplacedStencilDraw",
    };

    [ViewVariables] public readonly Dictionary<object, ColorControlTrackedLayer> TrackedLayers =
        new(ReferenceEqualityComparer.Instance);

    [ViewVariables] public readonly HashSet<object> IndexedLayers = new(ReferenceEqualityComparer.Instance);

    [ViewVariables] public List<int>? ResolvedIndices;

    [ViewVariables] public uint Frame;
}

/// <summary>
/// What <see cref="ColorControlComponent"/> changed on a layer, and what it was before.
/// </summary>
public sealed class ColorControlTrackedLayer
{
    [ViewVariables] public Color OriginalColor;
    [ViewVariables] public Color AppliedColor;
    [ViewVariables] public bool ColorApplied;

    [ViewVariables] public string? OriginalShaderProto;

    [ViewVariables] public object? OriginalShader;

    [ViewVariables] public string? AppliedShader;
    [ViewVariables] public bool ShaderApplied;

    [ViewVariables] public uint LastSeen;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ColorControlLayerOverride
{
    [DataField] public Color? Color;

    [DataField] public ColorControlMode? Mode;

    [DataField] public string? Shader;
}

public enum ColorControlMode : byte
{
    Replace,
    Multiply,
}

[Flags]
public enum ColorControlTargets : byte
{
    None = 0,
    Body = 1 << 0,
    Clothing = 1 << 1,
    Held = 1 << 2,
    All = Body | Clothing | Held,
}

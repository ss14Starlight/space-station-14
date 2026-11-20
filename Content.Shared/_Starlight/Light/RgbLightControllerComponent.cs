// ReSharper disable CheckNamespace
namespace Content.Shared.Light.Components;

public sealed partial class RgbLightControllerComponent
{
    [ViewVariables] public readonly Dictionary<object, Color> AutoOriginalColors = new(ReferenceEqualityComparer.Instance);
}

// ReSharper disable CheckNamespace
namespace Content.Shared.Chemistry.Components;

public sealed partial class SolutionContainerVisualsComponent
{
    /// <summary>
    /// The fill layer's state from before it first showed the empty sprite. Put back when a container with no fill
    /// sprites, like a medipen, is filled again.
    /// </summary>
    [ViewVariables]
    public string? FullSpriteName;
}

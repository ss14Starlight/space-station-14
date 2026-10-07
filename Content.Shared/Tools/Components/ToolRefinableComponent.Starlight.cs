namespace Content.Shared.Tools.Components;

public sealed partial class ToolRefinableComponent
{
    /// <summary>
    /// Consume only one item when refining a stack, leaving the remainder intact.
    /// </summary>
    [DataField]
    public bool RefineOneFromStack;
}

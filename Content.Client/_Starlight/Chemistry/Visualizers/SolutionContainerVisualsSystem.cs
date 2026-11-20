using Content.Shared.Chemistry.Components;
using Robust.Client.GameObjects;

// ReSharper disable CheckNamespace
namespace Content.Client.Chemistry.Visualizers;

public sealed partial class SolutionContainerVisualsSystem
{
    /// <summary>
    /// Keeps the fill layer's state from before it first switches to the empty sprite.
    /// </summary>
    private void RememberFullSprite(Entity<SpriteComponent?> sprite, SolutionContainerVisualsComponent component, int fillLayer)
    {
        if (component.FullSpriteName != null || component.Metamorphic)
            return;

        var state = SpriteSystem.LayerGetRsiState(sprite, fillLayer).Name;
        if (state != component.EmptySpriteName)
            component.FullSpriteName = state;
    }

    /// <summary>
    /// Puts the full sprite back on a container with no fill sprites, like a medipen, once it holds something again.
    /// </summary>
    private void RestoreFullSprite(Entity<SpriteComponent?> sprite, SolutionContainerVisualsComponent component, int fillLayer)
    {
        if (component.FullSpriteName != null)
            SpriteSystem.LayerSetRsiState(sprite, fillLayer, component.FullSpriteName);
    }
}

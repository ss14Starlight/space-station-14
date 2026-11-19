using System.Numerics;
using Content.Shared._Starlight.Silicons.Bots;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._Starlight.Silicons.Bots;

public sealed partial class MuleSystem : SharedMuleSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private const string LoadLayer = "mule-load-";

    /// <summary>
    /// How far up the load sits on the platform.
    /// </summary>
    private static readonly Vector2 LoadOffset = new(0f, 11f / 32f);

    [SubscribeLocalEvent]
    private void OnInserted(Entity<MuleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            UpdateLoad(ent);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<MuleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            UpdateLoad(ent);
    }

    /// <summary>
    /// Draws a copy of the load's sprite on top of the bot.
    /// </summary>
    private void UpdateLoad(Entity<MuleComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        for (var i = 0; _sprite.LayerExists((ent, sprite), LoadLayer + i); i++)
        {
            _sprite.RemoveLayer((ent, sprite), LoadLayer + i);
        }

        // Riders draw themselves; only what's on the platform is copied.
        if (!Container.TryGetContainer(ent, ent.Comp.ContainerId, out var container)
            || container.ContainedEntities.Count == 0
            || !TryComp<SpriteComponent>(container.ContainedEntities[0], out var loadSprite))
            return;

        var n = 0;
        foreach (var layer in loadSprite.AllLayers)
        {
            if (!layer.Visible || layer is not SpriteComponent.Layer { ActualRsi: { } rsi } source)
                continue;

            var index = _sprite.AddRsiLayer((ent, sprite), source.State, rsi);
            _sprite.LayerSetColor((ent, sprite), index, source.Color);
            _sprite.LayerSetOffset((ent, sprite), index, source.Offset + LoadOffset);
            _sprite.LayerMapSet((ent, sprite), LoadLayer + n++, index);
        }
    }
}

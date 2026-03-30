using System.Linq;
using System.Numerics;
using Content.Client._Moffstation.GameObjects;
using Content.Shared._Moffstation.Extensions;
using Content.Shared._Starlight.Economy.PokerChips.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Starlight.Economy.PokerChips;

public sealed class PokerChipStackVisualizerSystem : ManagedLayerVisualizerSystem<PokerChipStackComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override ref HashSet<string> GetSpriteLayersAdded(PokerChipStackComponent component) =>
        ref component.SpriteLayersAdded;

    protected override void AddLayersOnAppearanceChange(PokerChipStackComponent component,
        Entity<SpriteComponent?> sprite,
        AppearanceComponent appearance,
        LayerFactory layerFactory)
    {
        if (_timing.IsFirstTimePredicted)
            return;

        if (!AppearanceSystem.TryGetData<List<Entity<PokerChipComponent>>>(sprite, PokerChipStackVisuals.Chips,
                out var visibleChips, appearance))
            return;

        var rotation = Angle.FromDegrees(90);
        var scale = Vector2.One;
        foreach (var (chipIdx, chip) in visibleChips.Index())
        {
            if (!TryComp<SpriteComponent>(chip, out var cSprite)) return;

            var off = new Vector2(0, component.YOffset * chipIdx);
            foreach (var (currLayerIdx, curLayer) in cSprite.AllLayers.Index())
            {
                if (curLayer is not Layer layer) continue;
                layerFactory($"{chipIdx}-{currLayerIdx}", layer.ToPrototypeData().Plus(scale, rotation, off));
            }
        }
    }
}

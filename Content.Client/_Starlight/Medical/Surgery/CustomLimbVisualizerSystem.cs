using System.Linq;
using System.Numerics;
using Content.Client.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Item;
using Content.Shared.Starlight.Medical.Surgery;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Medical.Surgery;

public sealed class CustomLimbVisualizerSystem : EntitySystem
{
    [Dependency] private readonly DisplacementMapSystem _displacement = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomLimbVisualizerComponent, AfterAutoHandleStateEvent>(OnChanged);
    }

    private void OnChanged(Entity<CustomLimbVisualizerComponent> ent, ref AfterAutoHandleStateEvent _) => OnChanged(ent);

    private void OnChanged(Entity<CustomLimbVisualizerComponent> ent)
    {
        if (Deleted(ent.Owner) || !TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);
        var old = ent.Comp.CachedLayers.ToHashSet();
        var updatedLayers = new HashSet<HumanoidVisualLayers>();

        foreach (var item in ent.Comp.Layers)
        {
            if (!item.Value.HasValue)
                continue;

            var limb = GetEntity(item.Value);
            if (Deleted(limb) || !TryComp<SpriteComponent>(limb, out var layerSprite))
                continue;

            string? state = null;
            if (TryComp<ItemComponent>(limb, out var itemComp) && itemComp.HeldPrefix is not null)
                state = $"{itemComp.HeldPrefix}-";

            var offset = Vector2.Zero;
            switch (item.Key)
            {
                case HumanoidVisualLayers.LArm:
                case HumanoidVisualLayers.LHand:
                case HumanoidVisualLayers.LLeg:
                case HumanoidVisualLayers.LFoot:
                    state += "inhand-left";
                    break;
                case HumanoidVisualLayers.RArm:
                case HumanoidVisualLayers.RHand:
                case HumanoidVisualLayers.RLeg:
                case HumanoidVisualLayers.RFoot:
                    state += "inhand-right";
                    break;
            }

            if (state is null)
                continue;

            switch (item.Key)
            {
                case HumanoidVisualLayers.LArm:
                case HumanoidVisualLayers.LHand:
                case HumanoidVisualLayers.RArm:
                case HumanoidVisualLayers.RHand:
                    offset = new Vector2(0, item.Key is HumanoidVisualLayers.LHand or HumanoidVisualLayers.RHand ? 0.09375f : 0.1875f);
                    break;
                case HumanoidVisualLayers.LLeg:
                case HumanoidVisualLayers.RLeg:
                    offset = new Vector2(0, -0.15625f);
                    break;
                case HumanoidVisualLayers.LFoot:
                case HumanoidVisualLayers.RFoot:
                    offset = new Vector2(0, -0.34375f);
                    break;
            }

            if (layerSprite.BaseRSI?.TryGetState(state, out var rsiState) ?? false)
            {
                var index = _sprite.LayerMapReserve(spriteEnt, $"custom-{item.Key}");
                _sprite.LayerSetRsi(spriteEnt, index, layerSprite.BaseRSI, rsiState.StateId);
                _sprite.LayerSetOffset(spriteEnt, index, offset);
                _sprite.LayerSetVisible(spriteEnt, index, true);
                updatedLayers.Add(item.Key);
            }

            // if (ent.Comp.Displacements.TryGetValue(item.Key, out var displacementData) && !ent.Comp.CachedLayers.Contains($"{item.Key}-displacement"))
            // {
            //     sprite.LayerMapSet(item.Key.ToString(), (int)item.Key);
            //     _displacement.TryAddDisplacement(displacementData, sprite, (int)item.Key, item.Key.ToString(), ent.Comp.CachedLayers);
            // }
        }

        foreach (var layer in old)
        {
            if (updatedLayers.Contains(layer))
                continue;

            if (_sprite.LayerMapTryGet(spriteEnt, $"custom-{layer}", out var index, false))
                _sprite.LayerSetVisible(spriteEnt, index, false);
        }

        ent.Comp.CachedLayers = updatedLayers;
    }
}

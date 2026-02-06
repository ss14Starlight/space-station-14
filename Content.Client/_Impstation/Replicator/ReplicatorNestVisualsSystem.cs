using Content.Shared._Impstation.Replicator;
using Robust.Client.GameObjects;

namespace Content.Client._Impstation.Replicator;

public sealed partial class ReplicatorNestVisualsSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ReplicatorNestEmbiggenedEvent>(OnEmbiggened);
    }

    private void OnEmbiggened(ReplicatorNestEmbiggenedEvent ev, EntitySessionEventArgs args)
    {
        var ent = GetEntity(ev.Ent);
        
        if (!TryComp<ReplicatorNestComponent>(ent, out var nest) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        Enum targetLayer = nest.CurrentLevel switch
        {
            >= 3 => ReplicatorNestVisuals.Level3,
            2 => ReplicatorNestVisuals.Level2,
            _ => ReplicatorNestVisuals.Level1,
        };

        Enum targetLayerUnshaded = nest.CurrentLevel switch
        {
            >= 3 => ReplicatorNestVisuals.Level3Unshaded,
            2 => ReplicatorNestVisuals.Level2Unshaded,
            _ => ReplicatorNestVisuals.Level1Unshaded,
        };

        if (!_sprite.TryGetLayer(ent, targetLayer, out var layerIndex, false))
            return;

        if (!_sprite.TryGetLayer(ent, targetLayerUnshaded, out var layerIndexUnshaded, false))
            return;

        _sprite.LayerSetVisible(layerIndex, true);
        _sprite.LayerSetVisible(layerIndexUnshaded, true);

        _appearance.OnChangeData(ent, sprite);
    }
}

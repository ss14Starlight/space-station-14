// these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

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
        if (!TryComp<SpriteComponent>(ev.Ent, out var sprite))
            return;

        Enum targetLayer = ev.Ent.Comp.CurrentLevel switch
        {
            >= 3 => ReplicatorNestVisuals.Level3,
            2 => ReplicatorNestVisuals.Level2,
            _ => ReplicatorNestVisuals.Level1,
        };

        Enum targetLayerUnshaded = ev.Ent.Comp.CurrentLevel switch
        {
            >= 3 => ReplicatorNestVisuals.Level3Unshaded,
            2 => ReplicatorNestVisuals.Level2Unshaded,
            _ => ReplicatorNestVisuals.Level1Unshaded,
        };

        if (!_sprite.TryGetLayer(ev.Ent.Owner ,targetLayer, out var layerIndex, false))
            return;

        if (!_sprite.TryGetLayer(ev.Ent.Owner, targetLayerUnshaded, out var layerIndexUnshaded, false))
            return;

        _sprite.LayerSetVisible(layerIndex, true);
        _sprite.LayerSetVisible(layerIndexUnshaded, true);

        _appearance.OnChangeData(ev.Ent.Owner, sprite);
    }
}

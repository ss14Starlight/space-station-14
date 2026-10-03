using Content.Server.Xenoarchaeology.Artifact.XAT.Components;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Xenoarchaeology.Artifact.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Xenoarchaeology.Artifact.XAT;

public sealed partial class XATMagnetSystem
{
    private readonly HashSet<Entity<MagnetPickupComponent>> _magnetEntities = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        CheckActiveMagnets();
    }

    // Active magnetic inventories trigger the node too
    private void CheckActiveMagnets()
    {
        var query = EntityQueryEnumerator<XATMagnetComponent, XenoArtifactNodeComponent>();
        while (query.MoveNext(out var uid, out var comp, out var node))
        {
            if (node.Attached == null)
                continue;

            var artifact = _xenoArtifactQuery.Get(node.Attached.Value);

            if (!CanTrigger(artifact, (uid, node)))
                continue;

            var coords = Transform(artifact.Owner).Coordinates;

            _magnetEntities.Clear();
            _lookup.GetEntitiesInRange(coords, comp.MagbootsRange, _magnetEntities);
            foreach (var ent in _magnetEntities)
            {
                if (!TryComp<ItemToggleComponent>(ent, out var itemToggle) || !itemToggle.Activated)
                    continue;

                Trigger(artifact, (uid, comp, node));
                break;
            }
        }
    }
}

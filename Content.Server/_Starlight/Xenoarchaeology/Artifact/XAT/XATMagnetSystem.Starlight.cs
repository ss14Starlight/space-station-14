using Content.Server.Xenoarchaeology.Artifact.XAT.Components;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Xenoarchaeology.Artifact.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Xenoarchaeology.Artifact.XAT;

public sealed partial class XATMagnetSystem
{
    private HashSet<Entity<MagnetPickupComponent>> _magnetEntities = new();

    // Active magnetic inventories trigger the node too
    partial void CheckActiveMagnets(Entity<XenoArtifactComponent> artifact, Entity<XATMagnetComponent, XenoArtifactNodeComponent> node)
    {
        var coords = Transform(artifact.Owner).Coordinates;

        _magnetEntities.Clear();
        _lookup.GetEntitiesInRange(coords, node.Comp1.MagbootsRange, _magnetEntities);
        foreach (var ent in _magnetEntities)
        {
            if (!TryComp<ItemToggleComponent>(ent, out var itemToggle) || !itemToggle.Activated)
                continue;

            Trigger(artifact, node);
            break;
        }
    }
}

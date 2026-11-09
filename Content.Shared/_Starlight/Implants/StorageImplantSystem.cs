

using System.Linq;
using Content.Shared._Starlight.Cybernetics;
using Content.Shared.Storage;
using Content.Shared.Implants.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Implants;

public sealed partial class StorageImplantSystem
{
    private void OnCyberneticDisruption(Entity<StorageImplantComponent> ent, ref CyberneticDisruptionEvent args)
    {
        if (!_container.TryGetContainer(ent.Owner, StorageComponent.ContainerId, out var storageImplant))
            return;

        var contained = storageImplant.ContainedEntities.ToArray();
        foreach (var entity in contained)
        {
            _transform.DropNextTo(entity, args.Target);
        }
    }
}

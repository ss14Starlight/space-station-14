using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Central isolation check for patients zipped inside a Bioseal Rollerbed.
/// </summary>
public sealed partial class PathogenIsolationSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;

    public bool IsIsolated(EntityUid uid)
    {
        if (!_containers.TryGetContainingContainer((uid, null, null), out var container) ||
            !TryComp<BiosealRollerbedComponent>(container.Owner, out _) ||
            !TryComp<EntityStorageComponent>(container.Owner, out var storage))
        {
            return false;
        }

        return !storage.Open && storage.Contents.Contains(uid);
    }
}

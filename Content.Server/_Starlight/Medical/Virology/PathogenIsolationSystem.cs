using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Central isolation check for anything sealed inside a container that contains its
/// pathogens: the Bioseal Rollerbed, and body bags and morgues.
/// </summary>
public sealed partial class PathogenIsolationSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;

    public bool IsIsolated(EntityUid uid)
    {
        if (!_containers.TryGetContainingContainer((uid, null, null), out var container) ||
            !TryComp<EntityStorageComponent>(container.Owner, out var storage))
        {
            return false;
        }

        // The bioseal is purpose-built for this. Body bags and morgues qualify on the same
        // rule the rot contamination path already uses: a container sealed well enough to
        // stop a body decomposing is sealed well enough to keep its pathogens inside.
        if (!HasComp<BiosealRollerbedComponent>(container.Owner) &&
            !HasComp<AntiRottingContainerComponent>(container.Owner))
        {
            return false;
        }

        return !storage.Open && storage.Contents.Contains(uid);
    }
}

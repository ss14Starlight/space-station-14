using Content.Shared.PAI;
using Robust.Server.Containers;

// ReSharper disable once CheckNamespace
namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private ContainerSystem _containerSystem = default!;

    private bool IsSlottedPAI(EntityUid user, EntityUid console)
    {
        return HasComp<PAIComponent>(user) &&
               _containerSystem.TryGetContainingContainer(user, out var container) &&
               container.Owner == console &&
               container.ID == "pai_slot";
    }
}

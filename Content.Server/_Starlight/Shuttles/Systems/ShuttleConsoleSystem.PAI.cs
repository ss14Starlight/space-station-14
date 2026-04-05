using Robust.Server.Containers;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private readonly ContainerSystem _containerSystem = default!;

    private bool IsSlottedPAI(EntityUid user, EntityUid console)
    {
        return _containerSystem.TryGetContainingContainer(user, out var container) &&
               container != null &&
               container.Owner == console &&
               container.ID == "pai_slot";
    }
}

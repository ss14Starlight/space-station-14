using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;
using Content.Shared.DoAfter;

// ReSharper disable CheckNamespace
namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class InjectorSystem
{
    private bool HasPendingDoAfter(Entity<InjectorComponent> injector, EntityUid user)
    {
        if (!TryComp<DoAfterComponent>(user, out var doAfters))
            return false;

        foreach (var doAfter in doAfters.DoAfters.Values)
        {
            if (!doAfter.Cancelled
                && !doAfter.Completed
                && doAfter.Args.Used == injector.Owner
                && doAfter.Args.Event is InjectorDoAfterEvent)
                return true;
        }

        return false;
    }
}

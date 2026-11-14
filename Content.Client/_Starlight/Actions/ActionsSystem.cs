using Content.Shared.Actions.Components;

// ReSharper disable CheckNamespace
namespace Content.Client.Actions;

public sealed partial class ActionsSystem
{
    private static bool IsExternalAction(Entity<ActionComponent> action)
        => action.Comp.Container != null && action.Comp.Container != action.Comp.AttachedEntity;
}

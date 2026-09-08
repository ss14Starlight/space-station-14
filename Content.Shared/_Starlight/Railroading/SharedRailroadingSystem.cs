using Content.Shared._Starlight.Railroading.Components;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Shared._Starlight.Railroading;

public abstract partial class SharedRailroadingSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    public void InvalidateProgress(Entity<RailroadableComponent> ent)
    {
        if (ent.Comp.ActiveCard is null)
            return;

        var @event = new RailroadingCardCompletionQueryEvent();
        RaiseLocalEvent(ent.Comp.ActiveCard.Value, ref @event);
        if (@event.IsCompleted != true)
            return;

        var completedEvent = new RailroadingCardCompletedEvent(ent);
        RaiseLocalEvent(ent.Comp.ActiveCard.Value, ref completedEvent);

        _adminLog.Add(LogType.Railroading, LogImpact.Medium, $"{ToPrettyString(ent)} completed card {ToPrettyString(ent.Comp.ActiveCard.Value)}.");
        ent.Comp.Completed ??= [];
        ent.Comp.Completed.Add(ent.Comp.ActiveCard.Value);
        ent.Comp.ActiveCard = null;
    }
}

using System;
using Content.Server._Starlight.Utility;
using Content.Server._Starlight.Utility.Events;
using Content.Shared.Singularity.Components;

// ReSharper disable CheckNamespace
namespace Content.Server.Singularity.EntitySystems;

public sealed partial class EmitterSystem
{
    [Dependency] private DelayedEventSystem _delayedEvent = default!;

    private const string UnpoweredAlertEventId = "emitter-unpowered-alert";

    private void ScheduleUnpoweredAlert(EntityUid uid, EmitterComponent component)
    {
        if (!component.IsOn || !component.AlertRadio)
            return;

        _delayedEvent.Schedule(uid, UnpoweredAlertEventId, TimeSpan.FromSeconds(3));
    }

    [SubscribeLocalEvent]
    private void OnDelayedEventTriggered(Entity<EmitterComponent> ent, ref DelayedEventTriggeredEvent args)
    {
        if (args.EventId != UnpoweredAlertEventId
            || !ent.Comp.IsOn
            || ent.Comp.IsPowered
            || !ent.Comp.AlertRadio)
            return;

        AlertRadio((ent.Owner, ent.Comp), ent.Comp.LocUnpowered, requirePowered: false);
    }

    [SubscribeLocalEvent]
    private void OnComponentShutdown(Entity<EmitterComponent> ent, ref ComponentShutdown args)
        => CancelUnpoweredAlert(ent.Owner);

    private void CancelUnpoweredAlert(EntityUid uid)
        => _delayedEvent.Cancel(uid, UnpoweredAlertEventId);
}

using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Atmos.AirlockCycler;

public sealed partial class AirlockCyclerSystem
{
    [SubscribeLocalEvent]
    private void OnLinkAttempt(Entity<AirlockCyclerComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.Source != ent.Owner || !IsCyclerPort(ent.Comp, args.SourcePort))
            return;

        // Only vents can fill the chamber; other devices on the inlets would never run.
        if ((args.SourcePort == ent.Comp.InletPort && !IsInletDevice(args.Sink))
            || (args.SourcePort == ent.Comp.OutletPort && !IsOutletDevice(args.Sink)))
        {
            args.Cancel();
            return;
        }

        if (!TryComp<DeviceLinkSinkComponent>(args.Sink, out var sink))
            return;

        foreach (var other in sink.LinkedSources)
        {
            if (!TryComp<AirlockCyclerComponent>(other, out var otherComp))
                continue;

            if (other != ent.Owner)
            {
                args.Cancel();
                return;
            }

            if (!TryComp<DeviceLinkSourceComponent>(other, out var source))
                continue;

            foreach (var (port, _) in _deviceLink.GetLinks(other, args.Sink, source))
            {
                // One device per role, except a vent that both fills and empties a small chamber.
                if (IsCyclerPort(otherComp, port)
                    && port != args.SourcePort
                    && !(IsDevicePort(otherComp, port) && IsDevicePort(otherComp, args.SourcePort)))
                {
                    args.Cancel();
                    return;
                }
            }
        }
    }

    private static bool IsCyclerPort(AirlockCyclerComponent comp, string port)
        => port == comp.InletPort || port == comp.OutletPort || comp.Sides.Contains(port);

    private bool OwnsDevice(EntityUid controller, EntityUid device)
    {
        if (!TryComp<DeviceLinkSinkComponent>(device, out var sink))
            return true;

        foreach (var other in sink.LinkedSources)
        {
            if (other != controller && other.Id < controller.Id && HasComp<AirlockCyclerComponent>(other))
                return false;
        }

        return true;
    }

    private HashSet<EntityUid> GetOwnedSinks(Entity<AirlockCyclerComponent> ent,
        DeviceLinkSourceComponent source,
        ProtoId<SourcePortPrototype> port)
    {
        var sinks = _deviceLink.GetLinkedSinks((ent, source), port);
        sinks.RemoveWhere(sink => !OwnsDevice(ent, sink));
        return sinks;
    }
}

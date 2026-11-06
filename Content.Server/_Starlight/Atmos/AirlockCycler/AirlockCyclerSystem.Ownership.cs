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
                if (IsCyclerPort(otherComp, port) && port != args.SourcePort)
                {
                    args.Cancel();
                    return;
                }
            }
        }
    }

    private static bool IsCyclerPort(AirlockCyclerComponent comp, string port)
        => port == comp.VentPort || comp.Sides.Contains(port);

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

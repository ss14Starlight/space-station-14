using Content.Server.Fax;
using Content.Shared._Starlight.Railroading.Components;
using Content.Shared._Starlight.Railroading.Components.Handlers.Fax;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Fax.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Railroading.HandlerSystem;

public sealed partial class RailroadingFaxHandlerSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadFaxOnChosenComponent, RailroadingCardChosenEvent>(OnChosen);
        SubscribeLocalEvent<RailroadFaxOnFailedComponent, RailroadingCardFailedEvent>(OnFailed);
    }

    private void OnFailed(Entity<RailroadFaxOnFailedComponent> ent, ref RailroadingCardFailedEvent args)
        => SendFax(ent.Comp, args.Subject);

    private void OnChosen(Entity<RailroadFaxOnChosenComponent> ent, ref RailroadingCardChosenEvent args)
    {
        if (ent.Comp.Delay <= TimeSpan.Zero)
        {
            SendFax(ent.Comp, args.Subject);
            return;
        }

        ent.Comp.SendAt = _timing.CurTime + ent.Comp.Delay;
        ent.Comp.PendingSubject = args.Subject;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RailroadFaxOnChosenComponent>();
        while (query.MoveNext(out var comp))
        {
            if (comp.SendAt is not { } sendAt || sendAt > now)
                continue;

            comp.SendAt = null;
            if (TryComp<RailroadableComponent>(comp.PendingSubject, out var railroadable))
                SendFax(comp, (comp.PendingSubject.Value, railroadable));
            comp.PendingSubject = null;
        }
    }

    private void SendFax(IRailroadFaxComponent component, Entity<RailroadableComponent> subject)
    {
        var letter = _random.Pick(component.Letters);

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = FaxConstants.FaxPrintCommand,
            [FaxConstants.FaxPaperNameData] = Loc.GetString(letter.PaperName, ("subject", subject)),
            [FaxConstants.FaxPaperContentData] = Loc.GetString(letter.PaperContent, ("subject", subject)),
            [FaxConstants.FaxPaperLabelData] = letter.PaperLabel,
            [FaxConstants.FaxPaperStampStateData] = letter.StampState,
            [FaxConstants.FaxPaperStampedByData] = letter.StampedBy,
            [FaxConstants.FaxPaperPrototypeData] = letter.PaperPrototype,
            [FaxConstants.FaxPaperLockedData] = letter.Locked
        };

        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var faxEnt, out var faxComp))
        {
            if (!component.Addresses.Contains(faxComp.FaxName))
                continue;
            var @event = new DeviceNetworkPacketEvent
                (
                    0,
                    null,
                    0,
                    "NT",
                    EntityUid.Invalid,
                    payload
                );
            RaiseLocalEvent(faxEnt, @event);
        }
    }
}

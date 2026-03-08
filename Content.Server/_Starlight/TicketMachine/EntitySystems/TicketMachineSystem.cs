using Content.Shared._Starlight.TicketMachine.Components;
using Content.Shared._Starlight.TicketMachine.EntitySystems;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Power.EntitySystems;
using Content.Server.Atmos.EntitySystems;

namespace Content.Server._Starlight.TicketMachine.EntitySystems;

public sealed class TicketMachineSystem : SharedTicketMachineSystem
{
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiverSystem = default!;
    [Dependency] private readonly FlammableSystem _flammableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
    
        //Device linking
        SubscribeLocalEvent<TicketMachineComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    #region Device Linking

    private void OnSignalReceived(EntityUid uid, TicketMachineComponent component, ref SignalReceivedEvent args)
    {
        if (args.Port == component.NextNumberPort && _powerReceiverSystem.IsPowered(uid) 
            && component.displayNumber < component.lastIssuedNumber) // You can't go higher than the number of issued tickets
        {
            component.displayNumber++;
            UpdateVisuals(uid, component);
            Dirty(uid, component);
        }
        else if (args.Port == component.BurnPort && _powerReceiverSystem.IsPowered(uid))
        {
            List<EntityUid> burnedTickets = new();
            foreach (var ticket in component.issuedTickets)
            {
                if (!Exists(ticket))
                {
                    burnedTickets.Add(ticket); // Clean up invalid tickets
                    continue;
                }

                if (TryComp<TicketComponent>(ticket, out var ticketComp) 
                    && ticketComp.Number > component.displayNumber) // Only burn tickets which are already served
                    continue;
                
                _flammableSystem.Ignite(ticket, uid);
                burnedTickets.Add(ticket);
            }
            foreach (var burned in burnedTickets)
                component.issuedTickets.Remove(burned);
            Dirty(uid, component);
        }
    }

    #endregion
}
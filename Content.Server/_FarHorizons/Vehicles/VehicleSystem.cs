using Content.Shared._FarHorizons.Vehicles.Components;
using System.Linq;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Verbs;
using Content.Server.Destructible;
using Content.Shared.Administration.Logs;
using Content.Shared._FarHorizons.Vehicles.Events;
using Content.Shared._FarHorizons.Vehicles;

namespace Content.Server._FarHorizons.Vehicles;

public sealed partial class VehicleSystems : SharedVehicleSystem
{    
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleContainerComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<VehicleContainerComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
    }

    private void OnAlternativeVerb(EntityUid uid, VehicleContainerComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        if(!TryComp<VehicleComponent>(uid, out var vehicleComp)) return; 
        if(TryComp<DestructibleComponent>(uid, out var destructibleComp) && destructibleComp.IsBroken) return;

        if (CanInsert(uid, component) && !component.PassengerSlot.ContainedEntities.Contains(args.User))
        {
            var enterVerb = new AlternativeVerb
            {
                Text = Loc.GetString("vehicle-verb-enter"),
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.EntryTime, new VehicleEntryDoAfter(), uid, target: args.User)
                    {
                        BreakOnMove = true,
                    };
                    
                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(enterVerb);
        }
        else if(component.PassengerSlot.ContainedEntities.Contains(args.User))
        {
            var exitVerb = new AlternativeVerb
            {
                Text = Loc.GetString("vehicle-verb-leave"),
                Act = () =>
                {
                    TryRemove(args.User, uid, component);
                    if(HasComp<RiderComponent>(args.User))
                        RemoveRider(args.User, uid, vehicleComp);
                }
            };
            args.Verbs.Add(exitVerb);
        }
        
        if(component.PassengerSlot.ContainedEntities.Count != 0 && !component.PassengerSlot.ContainedEntities.Contains(args.User))
        {
            var removeVerb = new AlternativeVerb
            {
                Text = Loc.GetString("vehicle-verb-remove"),
                Act = () =>
                {
                    _popup.PopupEntity(Loc.GetString("vehicle-remove-passenger-attempt"), uid, PopupType.LargeCaution);
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.RemoveTime, new VehicleRemoveDoAfter(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    _adminLogger.Add(Shared.Database.LogType.Verb, Shared.Database.LogImpact.Medium, $"{ToPrettyString(args.User)} attempted to remove a passenger from {ToPrettyString(uid)}");

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(removeVerb);
        }
    }
    
    private void OnDragDrop(Entity<VehicleContainerComponent> ent, ref DragDropTargetEvent args)
    {
        if(args.Handled) return;
        args.Handled = true;
        if(TryComp<DestructibleComponent>(ent.Owner, out var destructibleComp) && destructibleComp.IsBroken) return;

        if(!CanInsert(ent.Owner, ent.Comp)) return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.EntryTime, new VehicleEntryDoAfter(), ent.Owner, target: args.Dragged)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }
}
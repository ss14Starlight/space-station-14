using System.Linq;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Server.Hands.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Whitelist;
using Content.Shared.Interaction.Components;
using Content.Shared._Starlight.Mech.Components;
using Content.Shared.Mech;
using Robust.Shared.Containers;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles the insertion of mech equipment into mechs.
/// </summary>
public sealed class MechEquipmentSystem : EntitySystem
{
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MechEquipmentComponent, AfterInteractEvent>(OnUsed);
        SubscribeLocalEvent<MechEquipmentComponent, InsertEquipmentEvent>(OnInsertEquipment);

        SubscribeLocalEvent<MechEquipmentActionComponent, MechToggleEquipmentEvent>(OnToggleEquipment);

    }

    private void OnUsed(EntityUid uid, MechEquipmentComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var mech = args.Target.Value;
        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        if (mechComp.Broken)
            return;

        if (args.User == mechComp.PilotSlot.ContainedEntity)
            return;
        
        if (!mechComp.MaintenanceMode)
        {
            _popup.PopupEntity("You need to turn on maintenance mode first!", args.User, PopupType.MediumCaution);
            return;
        }

        if (mechComp.EquipmentContainer.ContainedEntities.Count >= mechComp.MaxEquipmentAmount)
            return;

        if (_whitelistSystem.IsWhitelistFail(mechComp.EquipmentWhitelist, args.Used))
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-begin-install", ("item", uid)), mech);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.InstallDuration, new InsertEquipmentEvent(), uid, target: mech, used: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnInsertEquipment(EntityUid uid, MechEquipmentComponent component, InsertEquipmentEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-finish-install", ("item", uid)), args.Args.Target.Value);
        _mech.InsertEquipment(args.Args.Target.Value, uid);

        args.Handled = true;
    }

    private void OnToggleEquipment(EntityUid uid, MechEquipmentActionComponent component, MechToggleEquipmentEvent args)
    {
        var mech = args.Performer;

        if (!TryComp<MechComponent>(mech, out var chassis))
            return;

        args.Toggle = true;
        args.Handled = true;

        component.EquipmentToggled = !component.EquipmentToggled;
        if (component.EquipmentToggled)
        {
            var ev = new MechActivateEquipmentEvent(mech);
            RaiseLocalEvent(uid, ref ev);
        }
        else
        {
            var ev = new MechDeactivateEquipmentEvent();
            RaiseLocalEvent(uid, ref ev);
        }
    }

    #region Auxiliary Equipment
    #endregion

    #region Active Equipment
    private void ProvideItems(EntityUid chassis, EntityUid uid, MechComponent? chassisComponent = null, MechActiveEquipComponent? component = null)
    {
        if (!Resolve(chassis, ref chassisComponent) || !Resolve(uid, ref component))
            return;

        if (!TryComp<HandsComponent>(chassis, out var hands))
            return;

        var xform = Transform(chassis);
        foreach (var itemProto in component.Items)
        {
            EntityUid item;

            if (!component.ItemsCreated)
            {
                item = Spawn(itemProto, xform.Coordinates);
            }
            else
            {
                item = component.ProvidedContainer.ContainedEntities
                    .FirstOrDefault(ent => Prototype(ent)?.ID == itemProto.Id);
                if (!item.IsValid())
                {
                    Log.Debug($"no items found: {component.ProvidedContainer.ContainedEntities.Count}");
                    continue;
                }

                _container.Remove(item, component.ProvidedContainer, force: true);
            }

            if (!item.IsValid())
            {
                Log.Debug("no valid item");
                continue;
            }

            var handId = $"{uid}-item{component.HandCounter}";
            component.HandCounter++;
            _hands.AddHand((chassis, hands), handId, HandLocation.Middle);
            _hands.DoPickup(chassis, handId, item, hands);
            EnsureComp<UnremoveableComponent>(item);
            component.ProvidedItems.Add(handId, item);
        }

        component.ItemsCreated = true;
    }

    private void RemoveProvidedItems(EntityUid chassis, EntityUid uid, MechComponent? chassisComponent = null, MechActiveEquipComponent? component = null)
    {
        if (!Resolve(chassis, ref chassisComponent) || !Resolve(uid, ref component))
            return;

        if (!TryComp<HandsComponent>(chassis, out var hands))
            return;

        if (TerminatingOrDeleted(uid))
        {
            foreach (var (hand, item) in component.ProvidedItems)
            {
                QueueDel(item);
                _hands.RemoveHand(chassis, hand);
            }
            component.ProvidedItems.Clear();
            return;
        }

        foreach (var (handId, item) in component.ProvidedItems)
        {
            if (LifeStage(item) <= EntityLifeStage.MapInitialized)
            {
                RemComp<UnremoveableComponent>(item);
                _container.Insert(item, component.ProvidedContainer);
            }
            _hands.RemoveHand(chassis, handId);
        }
        component.ProvidedItems.Clear();
    }
    #endregion
}

using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Xenobiology;

public sealed class ClawpackSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClawpackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClawpackComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ClawpackComponent, ClawpackToggleClawEvent>(OnClawpackToggle);
        SubscribeLocalEvent<ClawpackComponent, GotUnequippedEvent>(OnToggleableUnequip);
    }

    private void OnMapInit(Entity<ClawpackComponent> entity, ref MapInitEvent args)
    {
        entity.Comp.ClawContainer = _containerSystem.EnsureContainer<ContainerSlot>(entity.Owner, entity.Comp.ClawpackClawContainerId);
        var xform = Transform(entity.Owner);
        entity.Comp.ItemUid = Spawn(entity.Comp.ItemPrototype, xform.Coordinates);
        var attachedClaw = EnsureComp<AttachedClawComponent>(entity.Comp.ItemUid.Value);
        attachedClaw.AttachedUid = entity.Owner;
        Dirty(entity.Comp.ItemUid.Value, attachedClaw);
        _containerSystem.Insert(entity.Comp.ItemUid.Value, entity.Comp.ClawContainer, containerXform: xform);
        Dirty(entity.Owner, entity.Comp);
        
        _actionsSystem.AddAction(entity.Owner, ref entity.Comp.ActionEntity, out _, entity.Comp.Action);
    }
    
    private void OnGetActions(Entity<ClawpackComponent> entity,
        ref GetItemActionsEvent args)
    {
        if (entity.Comp.ActionEntity is not null && (args.SlotFlags & entity.Comp.RequiredFlags) == entity.Comp.RequiredFlags)
            args.AddAction(entity.Comp.ActionEntity);
    }

    private void OnClawpackToggle(Entity<ClawpackComponent> entity,
        ref ClawpackToggleClawEvent args)
    {
        if (entity.Comp.ClawContainer == null || entity.Comp.ItemUid == null) return;
        var userHasClaw = false;
        foreach (var item in _hands.EnumerateHeld(args.Performer))
        {
            if (item == entity.Comp.ItemUid.Value)
            {
                userHasClaw = true;
                break;
            }
        }

        if (userHasClaw)
        {
            RemComp<UnremoveableComponent>(entity.Comp.ItemUid.Value);
            _hands.TryDropIntoContainer(args.Performer, entity.Comp.ItemUid.Value, entity.Comp.ClawContainer);
        }
        else
        {
            _containerSystem.Remove(entity.Comp.ItemUid.Value, entity.Comp.ClawContainer, force: true);
            if (!_hands.TryForcePickupAnyHand(args.Performer, entity.Comp.ItemUid.Value)) return;
            EnsureComp<UnremoveableComponent>(entity.Comp.ItemUid.Value);
            args.Handled = true;
        }
        Dirty(entity.Owner, entity.Comp);
    }
    
    private void OnToggleableUnequip(Entity<ClawpackComponent> entity,
        ref GotUnequippedEvent args)
    {
        if (entity.Comp.ClawContainer == null || entity.Comp.ItemUid == null) return;
        RemComp<UnremoveableComponent>(entity.Comp.ItemUid.Value);
        _hands.TryDropIntoContainer(args.Equipee, entity.Comp.ItemUid.Value, entity.Comp.ClawContainer);
    }
}

public sealed partial class ClawpackToggleClawEvent : InstantActionEvent
{
}
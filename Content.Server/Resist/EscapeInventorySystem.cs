using Content.Server.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Resist;
using Content.Shared.Storage;
using Content.Shared.Tag; // Starlight Edit
using Robust.Server.GameObjects; // Starlight Edit
using Robust.Shared.Containers;

namespace Content.Server.Resist;

public sealed class EscapeInventorySystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!; // Starlight Edit
    [Dependency] private readonly TransformSystem _transformSystem = default!; // Starlight Edit

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CanEscapeInventoryComponent, MoveInputEvent>(OnRelayMovement);
        SubscribeLocalEvent<CanEscapeInventoryComponent, EscapeInventoryEvent>(OnEscape);
        SubscribeLocalEvent<CanEscapeInventoryComponent, DroppedEvent>(OnDropped);
    }

    private void OnRelayMovement(EntityUid uid, CanEscapeInventoryComponent component, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (!_containerSystem.TryGetContainingContainer((uid, null, null), out var container) || !_actionBlockerSystem.CanInteract(uid, container.Owner))
            return;

        // Make sure there's nothing stopped the removal (like being glued)
        if (!_containerSystem.CanRemove(uid, container))
        {
            _popupSystem.PopupEntity(Loc.GetString("escape-inventory-component-failed-resisting"), uid, uid);
            return;
        }

        // Contested
        if (_handsSystem.IsHolding(container.Owner, uid, out _))
        {
            AttemptEscape(uid, container.Owner, component);
            return;
        }

        // Uncontested
        if (HasComp<StorageComponent>(container.Owner) || HasComp<InventoryComponent>(container.Owner) || HasComp<SecretStashComponent>(container.Owner))
        // Starlight edit start - Add another escapable container
        {
            AttemptEscape(uid, container.Owner, component);
            return;
        }
        
        // Uncontested - Escape from borg modules and such
        if (_tagSystem.HasTag(container.Owner, "PersonnelStorage"))
        {
            AttemptEscape(uid, container.Owner, component);
        }
        // Starlight edit end
    }

    private void AttemptEscape(EntityUid user, EntityUid container, CanEscapeInventoryComponent component, float multiplier = 1f)
    {
        if (component.IsEscaping)
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, component.BaseResistTime * multiplier, new EscapeInventoryEvent(), user, target: container)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false
        };

        if (!_doAfterSystem.TryStartDoAfter(doAfterEventArgs, out component.DoAfter))
            return;

        _popupSystem.PopupEntity(Loc.GetString("escape-inventory-component-start-resisting"), user, user);
        _popupSystem.PopupEntity(Loc.GetString("escape-inventory-component-start-resisting-target"), container, container);
    }

    private void OnEscape(EntityUid uid, CanEscapeInventoryComponent component, EscapeInventoryEvent args)
    {
        component.DoAfter = null;

        if (args.Handled || args.Cancelled)
            return;

        // Starlight edit start - Special handling for borg modules
        if (_containerSystem.TryGetContainingContainer((uid, null, null), out var container) &&
            _tagSystem.HasTag(container.Owner, "PersonnelStorage"))
        {
            // Remove from the container and put on the floor
            _containerSystem.Remove((uid, Transform(uid)), container, reparent: false);
            _transformSystem.AttachToGridOrMap(uid, Transform(uid));
        }
        else
        {
            _containerSystem.AttachParentToContainerOrGrid((uid, Transform(uid)));
        }
        // Starlight edit end
        args.Handled = true;
    }

    private void OnDropped(EntityUid uid, CanEscapeInventoryComponent component, DroppedEvent args)
    {
        if (component.DoAfter != null)
            _doAfterSystem.Cancel(component.DoAfter);
    }
}

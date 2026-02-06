using Content.Server._Starlight.Equipment.Components;
using Content.Server.Interaction;
using Content.Shared.PowerCell;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Wall;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Equipment.EntitySystems;

/// <summary>
/// Handles <see cref="LargeGrabberComponent"/> and all related UI logic
/// Adapted from <see cref="MechGrabberSystem"/>
/// </summary>
public sealed class LargeGrabberSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<LargeGrabberComponent, ComponentStartup>(OnStartup);

        SubscribeLocalEvent<LargeGrabberComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<LargeGrabberComponent, GrabberDoAfterEvent>(OnGrab);
    }

    /// <summary>
    /// Removes an item from the grabber's container
    /// </summary>
    /// <param name="uid">The grabber</param>
    /// <param name="user">The entity holding it</param>
    /// <param name="toRemove">The item being removed</param>
    /// <param name="component"></param>
    public void RemoveItem(EntityUid uid, EntityUid user, EntityUid toRemove, LargeGrabberComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _container.Remove(toRemove, component.ItemContainer);
        var userxform = Transform(user);
        var xform = Transform(toRemove);
        _transform.AttachToGridOrMap(toRemove, xform);
        var (userPos, userRot) = _transform.GetWorldPositionRotation(userxform);

        var offset = userPos + userRot.RotateVec(component.DepositOffset);
        _transform.SetWorldPositionRotation(toRemove, offset, Angle.Zero);
    }

    private void OnStartup(EntityUid uid, LargeGrabberComponent component, ComponentStartup args)
    {
        component.ItemContainer = _container.EnsureContainer<Container>(uid, "item-container");
    }

    private void OnInteract(EntityUid uid, LargeGrabberComponent component, AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (!_itemToggle.IsActivated(uid) && !(component.ItemContainer.ContainedEntities.Count >= component.MaxContents))
        {
            var target = args.Target ?? args.User;

            if (target == args.User || component.DoAfter != null)
                return;

            if (TryComp<PhysicsComponent>(target, out var physics) && physics.BodyType == BodyType.Static ||
                HasComp<WallMountComponent>(target) ||
                HasComp<MobStateComponent>(target))
            {
                return;
            }

            if (Transform(target).Anchored)
                return;

            if (!_interaction.InRangeUnobstructed(args.User, target))
                return;

            if (!_powerCell.TryUseCharge(uid, component.GrabEnergyCost))
                return;

            args.Handled = true;
            component.AudioStream = _audio.PlayPvs(component.GrabSound, uid)?.Entity;
            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.GrabDelay, new GrabberDoAfterEvent(), uid, target: target, used: uid)
            {
                BreakOnMove = true
            };

            _doAfter.TryStartDoAfter(doAfterArgs, out component.DoAfter);
        }
        else if (_itemToggle.IsActivated(uid))
        {
            var targetCoords = new EntityCoordinates(args.User, component.DepositOffset);
            if (!_interaction.InRangeUnobstructed(args.User, targetCoords))
                return;

            if (component.ItemContainer.ContainedEntities.TryFirstOrNull(out var item) && item.HasValue)
                RemoveItem(uid, args.User, item.Value, component);
            UpdateState(uid, component);
            args.Handled = true;
        }
    }

    private void OnGrab(EntityUid uid, LargeGrabberComponent component, DoAfterEvent args)
    {
        component.DoAfter = null;

        if (args.Cancelled)
        {
            component.AudioStream = _audio.Stop(component.AudioStream);
            return;
        }

        if (args.Handled || args.Args.Target == null)
            return;

        _container.Insert(args.Args.Target.Value, component.ItemContainer);
        UpdateState(uid, component);

        args.Handled = true;
    }

    private void UpdateState(EntityUid uid, LargeGrabberComponent component)
    {
        if (component.ItemContainer.ContainedEntities.Count <= 0)
            _itemToggle.TryDeactivate(uid);
        else if (component.ItemContainer.ContainedEntities.Count >= component.MaxContents)
            _itemToggle.TryActivate(uid);
    }
}

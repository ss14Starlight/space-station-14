using Content.Server.Administration.Logs;
using Content.Server.Hands.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
// Starlight Start
using System.Linq;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Content.Shared.Verbs;
using Content.Shared.Holiday.Christmas;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
// Starlight End

namespace Content.Server.Holiday.Christmas;

/// <summary>
/// This handles granting players their gift.
/// </summary>
public sealed class RandomGiftSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    // Starlight Start
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    // Starlight End

    private readonly List<string> _possibleGiftsSafe = new();
    private readonly List<string> _possibleGiftsUnsafe = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<RandomGiftComponent, MapInitEvent>(OnGiftMapInit);
        SubscribeLocalEvent<RandomGiftComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RandomGiftComponent, ExaminedEvent>(OnExamined);

        // Starlight Start
        SubscribeLocalEvent<PresentComponent, GetVerbsEvent<AlternativeVerb>>(AddUnwrapVerb);
        SubscribeLocalEvent<PresentComponent, ActivateInWorldEvent>(OnPresentActivate);
        // Gift wrapping
        SubscribeLocalEvent<GiftWrapComponent, AfterInteractEvent>(OnGiftWrapAfterInteract);
        SubscribeLocalEvent<GiftWrapComponent, GiftWrapDoAfterEvent>(OnGiftWrapDoAfter);
        // Starlight End

        BuildIndex();
    }

    private void OnExamined(EntityUid uid, RandomGiftComponent component, ExaminedEvent args)
    {
        if (_whitelistSystem.IsWhitelistFail(component.ContentsViewers, args.Examiner) || component.SelectedEntity is null)
            return;

        var name = _prototype.Index<EntityPrototype>(component.SelectedEntity).Name;
        args.PushText(Loc.GetString("gift-packin-contains", ("name", name)));
    }

    // Starlight Start
    private void AddUnwrapVerb(EntityUid uid, PresentComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => UnwrapPresent(uid, args.User),
            Text = Loc.GetString("gift-unwrap-verb"),
            Priority = 1
        });
    }

    private void UnwrapPresent(EntityUid uid, EntityUid user)
    {
        var coords = Transform(user).Coordinates;
        // Check if this is a wrapped present
        if (_container.TryGetContainer(uid, "present_container", out var container))
        {
            // Empty the container and give items to user
            var items = container.ContainedEntities.ToList();
            foreach (var item in items)
            {
                _container.Remove(item, container);
                _hands.PickupOrDrop(user, item);
            }
        }
        // Check if this is a RandomGift present
        else if (TryComp<RandomGiftComponent>(uid, out var randomGift) && randomGift.SelectedEntity != null)
        {
            var handsEnt = Spawn(randomGift.SelectedEntity, coords);
            _hands.PickupOrDrop(user, handsEnt);
            if (randomGift.Wrapper != null)
                Spawn(randomGift.Wrapper, coords);
        }
        else
        {
            // Empty present
            var wrapper = Spawn("PresentTrash", coords);
            _hands.PickupOrDrop(user, wrapper);
        }
        // Play unwrap sound and delete present
        _audioSystem.PlayPvs(new SoundPathSpecifier("/Audio/Effects/unwrap.ogg"), uid);
        Del(uid);
    }

    private void OnPresentActivate(EntityUid uid, PresentComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        if (HasComp<RandomGiftComponent>(uid))
            return;
        UnwrapPresent(uid, args.User);
        args.Handled = true;
    }
    // Starlight End

    private void OnUseInHand(EntityUid uid, RandomGiftComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (component.SelectedEntity is null)
            return;

        var coords = Transform(args.User).Coordinates;
        var handsEnt = Spawn(component.SelectedEntity, coords);
        _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low, $"{ToPrettyString(args.User)} used {ToPrettyString(uid)} which spawned {ToPrettyString(handsEnt)}");
        if (component.Wrapper is not null)
            Spawn(component.Wrapper, coords);

        _audio.PlayPvs(component.Sound, args.User);

        // Don't delete the entity in the event bus, so we queue it for deletion.
        // We need the free hand for the new item, so we send it to nullspace.
        _transform.DetachEntity(uid, Transform(uid));
        QueueDel(uid);

        _hands.PickupOrDrop(args.User, handsEnt);

        args.Handled = true;
    }

    private void OnGiftMapInit(EntityUid uid, RandomGiftComponent component, MapInitEvent args)
    {
        if (component.InsaneMode)
            component.SelectedEntity = _random.Pick(_possibleGiftsUnsafe);
        else
            component.SelectedEntity = _random.Pick(_possibleGiftsSafe);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs obj)
    {
        if (obj.WasModified<EntityPrototype>())
            BuildIndex();
    }

    private void BuildIndex()
    {
        _possibleGiftsSafe.Clear();
        _possibleGiftsUnsafe.Clear();
        var itemCompName = Factory.GetComponentName<ItemComponent>();
        var mapGridCompName = Factory.GetComponentName<MapGridComponent>();
        var physicsCompName = Factory.GetComponentName<PhysicsComponent>();

        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || proto.HideSpawnMenu || proto.Components.ContainsKey(mapGridCompName) || !proto.Components.ContainsKey(physicsCompName))
                continue;

            _possibleGiftsUnsafe.Add(proto.ID);

            if (!proto.Components.ContainsKey(itemCompName))
                continue;

            _possibleGiftsSafe.Add(proto.ID);
        }
    }

    // Starlight Start: Gift wrapping
    private void OnGiftWrapAfterInteract(EntityUid uid, GiftWrapComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (!IsWrappable(component, target))
            return;

        args.Handled = TryStartWrapDoAfter(args.User, uid, component, target);
    }

    private bool IsWrappable(GiftWrapComponent component, EntityUid target)
    {
        // Must have an item component
        if (!HasComp<ItemComponent>(target))
            return false;
        // Check whitelist
        if (component.Whitelist != null && !_whitelistSystem.IsValid(component.Whitelist, target))
            return false;
        // Check blacklist
        if (component.Blacklist != null && _whitelistSystem.IsValid(component.Blacklist, target))
            return false;
        // Precent ception
        if (HasComp<PresentComponent>(target))
            return false;
        return true;
    }

    private bool TryStartWrapDoAfter(EntityUid user, EntityUid wrapper, GiftWrapComponent component, EntityUid target) =>
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            user,
            component.WrapDelay,
            new GiftWrapDoAfterEvent(),
            wrapper,
            target,
            wrapper)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
        });

    private void OnGiftWrapDoAfter(EntityUid uid, GiftWrapComponent component, GiftWrapDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;
        // Spawn the present
        var present = Spawn(component.PresentPrototype, Transform(target).Coordinates);
        // Create container and insert items
        var container = _container.EnsureContainer<Container>(present, "present_container");
        // Insert the wrapped item
        _container.Insert(target, container);
        // Insert the wrapping paper
        _container.Insert(uid, container);
        _audioSystem.PlayPvs(component.WrapSound, present);
        _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low,
            $"{ToPrettyString(args.User)} wrapped {ToPrettyString(target)} into {ToPrettyString(present)}");
        args.Handled = true;
    }
    // Starlight End
}

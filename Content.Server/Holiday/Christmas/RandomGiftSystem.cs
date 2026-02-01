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
using Content.Shared.Verbs;
using Content.Shared.Interaction;
using Robust.Shared.Map;

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

    private readonly List<string> _possibleGiftsSafe = new();
    private readonly List<string> _possibleGiftsUnsafe = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<RandomGiftComponent, MapInitEvent>(OnGiftMapInit);
        SubscribeLocalEvent<RandomGiftComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RandomGiftComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RandomGiftComponent, GetVerbsEvent<AlternativeVerb>>(OnGetWrapperVerbs); // 🌟Starlight🌟
        BuildIndex();
    }

    private void OnExamined(EntityUid uid, RandomGiftComponent component, ExaminedEvent args)
    {
        if (_whitelistSystem.IsWhitelistFail(component.ContentsViewers, args.Examiner) || component.SelectedEntity is null)
            return;

        var name = _prototype.Index<EntityPrototype>(component.SelectedEntity).Name;
        args.PushText(Loc.GetString("gift-packin-contains", ("name", name)));
    }

    private void OnUseInHand(EntityUid uid, RandomGiftComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

    // 🌟Starlight🌟 start
    //Functionality was moved and slightly modified
        var coords = Transform(args.User).Coordinates;
        SpawnItems(uid, component, args.User, coords);
        args.Handled = true;
    }

    private void SpawnItems(EntityUid uid, RandomGiftComponent component, EntityUid user, EntityCoordinates coordinates)
    {
        if (component.SelectedEntity is null)
            return;

        var handsEnt = Spawn(component.SelectedEntity, coordinates);
        _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low, $"{ToPrettyString(user)} used {ToPrettyString(uid)} which spawned {ToPrettyString(handsEnt)}");
        if (component.Wrapper is not null)
            Spawn(component.Wrapper, coordinates);

        _audio.PlayPvs(component.Sound, user);

        // Don't delete the entity in the event bus, so we queue it for deletion.
        // We need the free hand for the new item, so we send it to nullspace.
        _transform.DetachEntity(uid, Transform(uid));
        QueueDel(uid);

        _hands.PickupOrDrop(user, handsEnt);
    }

    private void OnGetWrapperVerbs(EntityUid uid, RandomGiftComponent component, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || (component.RequireHands && args.Hands != null) )
            return;

        if (_hands.IsHolding(args.User, uid))
            return;

        var user = args.User;
        var coords = Transform(uid).Coordinates;

        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => SpawnItems(uid, component, user, coords),
            Text = Loc.GetString("delivery-open-verb"),
        });
    }
    // 🌟Starlight🌟 end

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
}

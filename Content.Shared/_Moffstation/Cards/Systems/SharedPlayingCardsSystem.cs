using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Extensions;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Moffstation.Cards.Systems;

/// This system implements behaviors for playing cards, including decks, hands, and cards themselves.
/// <seealso cref="PlayingCardComponent"/>
/// <seealso cref="PlayingCardDeckComponent"/>
/// <seealso cref="PlayingCardHandComponent"/>
// This part just declares dependencies and has basic shared functions.
public abstract partial class SharedPlayingCardsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IComponentFactory _compFact = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    /// The priority of verbs for placing cards, should be high so that alt+clicking things always tries to do these.
    private const int PlacementVerbPriority = 100;

    /// <inheritdoc/>
    public override void Initialize()
    {
        InitCard();
        InitDeck();
        InitHand();
    }

    /// This function retrieves the <see cref="PlayingCardComponent"/> data for the given <paramref name="card"/>. Note
    /// that since <paramref name="card"/> may not be a spawned entity, the component may not be owned by an entity.
    /// Returns null in various cases if something goes wrong with resolving prototypes, net entities, etc.
    /// As of writing, this is used for retrieving user-facing information like examine text and visuals for hands/decks.
    public PlayingCardComponent? GetComponent(PlayingCardInDeck card)
    {
        var ret = card switch
        {
            // As of writing, this is only used for visuals / info, so we tolerate missing entities.
            PlayingCardInDeckNetEnt(var netEntity) => NetEntToCard(netEntity)?.Comp,
            PlayingCardInDeckUnspawnedData data => ToComponent(data),
            PlayingCardInDeckUnspawnedRef(var entProtoId, var faceDown) =>
                _proto.Resolve(entProtoId, out var proto) &&
                proto.Components.TryGetComponent<PlayingCardComponent>(_compFact, out var cardComp)
                    ? WithFacing(cardComp, faceDown)
                    : null,
            _ => card.ThrowUnknownInheritor<PlayingCardInDeck, PlayingCardComponent?>(),
        };
        if (ret is null)
        {
            return this.AssertOrLogError<PlayingCardComponent?>(
                $"Failed to get {nameof(PlayingCardComponent)} from {card}",
                null
            );
        }

        return ret;
    }

    /// Returns null in the exceptional case that the net ent can't be resolved to an entity.
    private Entity<PlayingCardComponent>? NetEntToCard(NetEntity netEnt) =>
        CompOrNull<PlayingCardComponent>(GetEntity(netEnt)) ?? this.AssertOrLogError<Entity<PlayingCardComponent>?>(
            $"Net Entity ({netEnt}) is missing expected {nameof(PlayingCardComponent)} ({ToPrettyString(GetEntity(netEnt))})",
            null
        );

    /// This function just sets the given <paramref name="comp"/>'s <see cref="PlayingCardComponent.FaceDown"/> and
    /// returns the component. This is useful for setting the component's value inline.
    private static PlayingCardComponent WithFacing(PlayingCardComponent comp, bool faceDown)
    {
        comp.FaceDown = faceDown;
        return comp;
    }

    /// Returns the given <paramref name="entity"/> with its component of type <typeparamref name="TComp"/> or
    /// <c>null</c> if it does not have that component. I just wanna use this all over and I NEED IT.
    private new Entity<TComp>? CompOrNull<TComp>(EntityUid? entity) where TComp : Component =>
        entity is { } e && TryComp<TComp>(entity, out var comp) ? new Entity<TComp>(e, comp) : null;

    /// This convenience function is used to make implementing interactions between card entities easier. It's kind of
    /// like a <c>switch</c> which runs the given handlers when <paramref name="switchOn"/> has certain components.
    /// It's assumed that <paramref name="switchOn"/> does not have more than one playing card related component.
    /// Returns false if <paramref name="switchOn"/> does not have any playing card related components, or if it is the
    /// same entity as <paramref name="receiver"/>.
    private bool HandlePlayingCardComponents(
        EntityUid? switchOn,
        EntityUid receiver,
        Action<Entity<PlayingCardComponent>> onCard,
        Action<Entity<PlayingCardDeckComponent>> onDeck,
        Action<Entity<PlayingCardHandComponent>> onHand
    )
    {
        if (switchOn == receiver)
            return false;
        if (CompOrNull<PlayingCardComponent>(switchOn) is { } card)
        {
            onCard(card);
            return true;
        }

        if (CompOrNull<PlayingCardDeckComponent>(switchOn) is { } deck)
        {
            onDeck(deck);
            return true;
        }

        if (CompOrNull<PlayingCardHandComponent>(switchOn) is { } hand)
        {
            onHand(hand);
            return true;
        }

        return false;
    }

    /// This is just a variant of <see cref="HandlePlayingCardComponents(EntityUid?,EntityUid,Action{Entity{PlayingCardComponent}},Action{Entity{PlayingCardDeckComponent}},Action{Entity{PlayingCardHandComponent}})"/>
    /// which coalesces the handling of decks and hands into a single handler for stacks.
    private bool HandlePlayingCardComponents(
        EntityUid? switchOn,
        EntityUid receiver,
        Action<Entity<PlayingCardComponent>> onCard,
        Action<Entity<PlayingCardStackComponent>> onStack
    ) => HandlePlayingCardComponents(
        switchOn,
        receiver,
        onCard,
        deck => onStack((deck, deck)), // Type safety is my passion :^)
        hand => onStack((hand, hand))
    );

    /// Runs <paramref name="action"/> if <paramref name="user"/> can pick up <paramref name="subject"/> to its hand
    /// with <paramref name="handId"/> (or its currently active hand if <c>null</c>).
    private bool PerformIfCanPickUp(EntityUid user, EntityUid subject, Func<EntityUid?> action, string? handId = null)
    {
        Entity<HandsComponent?> userHands = new(user, null);
        if ((handId ?? _hands.GetActiveHand(userHands)) is not { } hand ||
            !_hands.CanPickupToHand(userHands, subject, hand, handsComp: userHands))
            return false;

        return action() is { } toPickup &&
               _hands.TryPickup(userHands, toPickup, animate: false, handsComp: userHands, handId: hand);
    }

    private void VerbAudioAndPopup(VerbInfo info, EntityUid target, EntityUid user)
    {
        (string, object)[] locArgs = [("target", Name(target)), ("user", Identity.Name(user, EntityManager))];
        _popup.PopupPredicted(
            info.Popup(locArgs),
            info.Popup(locArgs),
            target,
            user
        );
        _audio.PlayPredicted(info.Sound, target, user, AudioVariation);
    }
}

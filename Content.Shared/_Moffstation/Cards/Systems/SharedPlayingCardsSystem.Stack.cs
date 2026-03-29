using System.Linq;
using Content.Shared._Moffstation.Cards.Components;
using Content.Shared._Moffstation.Cards.Events;
using Content.Shared._Moffstation.Extensions;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Random.Helpers;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using static Content.Shared._Moffstation.Cards.Components.PlayingCardStackComponent.Verbs;

namespace Content.Shared._Moffstation.Cards.Systems;

// This part handles behavior common to all PlayingCardStackComponent-derived components.
public abstract partial class SharedPlayingCardsSystem
{
    private static readonly AudioParams AudioVariation = AudioParams.Default.WithVariation(0.05f);

    private static void DirtyVisuals<TStack, TArgs>(Entity<TStack> entity, ref TArgs args)
        where TStack : PlayingCardStackComponent
    {
        entity.Comp.DirtyVisuals = true;
    }

    private void OnStartup<TStack>(Entity<TStack> entity, ref ComponentStartup args)
        where TStack : PlayingCardStackComponent
    {
        entity.Comp.Container = _container.EnsureContainer<Container>(entity, entity.Comp.ContainerId);
    }

    private void OnExamined<TStack>(Entity<TStack> entity, ref ExaminedEvent args)
        where TStack : PlayingCardStackComponent
    {
        args.PushText(Loc.GetString(PlayingCardStackComponent.ExamineText, ("count", entity.Comp.NumCards)));
    }

    /// These verbs are available when right-clicking on an entity while holding a stack.
    private void OnGetUtilityVerbsStack<TStack>(
        Entity<TStack> usedStack,
        ref GetVerbsEvent<UtilityVerb> args
    ) where TStack : PlayingCardStackComponent
    {
        var verbs = args.Verbs;
        var user = args.User;
        HandlePlayingCardComponents(
            args.Target,
            usedStack,
            // Pick up card onto used stack.
            targetCard => verbs.Add(PlayingCardComponent.Verbs.StackPickup, () => Add(usedStack, targetCard, user)),
            targetDeck =>
            {
                // Pick up top card onto used stack.
                verbs.Add(PlayingCardDeckComponent.Verbs.StackPickup,
                    () => TransferTopCard(targetDeck, usedStack, user));

                // Join target hand to held stack
                verbs.Add(PlayingCardHandComponent.Verbs.StackPickupEntire,
                    () => Transfer(targetDeck, usedStack, .., user));
            },
            targetHand =>
            {
                // Pick up picked card onto used stack.
                verbs.Add(PlayingCardHandComponent.Verbs.StackPickup, () => OpenPickerUi(targetHand, usedStack, user));
                // Join target hand to held stack
                verbs.Add(PlayingCardHandComponent.Verbs.StackPickupEntire,
                    () => Transfer(targetHand, usedStack, .., user));
            }
        );
    }

    private void OnGetAlternativeVerbsStack<TStack>(Entity<TStack> targetStack, ref GetVerbsEvent<AlternativeVerb> args)
        where TStack : PlayingCardStackComponent
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            args.Hands == null)
            return;

        var verbs = args.Verbs;
        var user = args.User;
        HandlePlayingCardComponents(
            args.Using,
            targetStack,
            // Add the used card into the target stack
            usedCard => verbs.Add(CardPutDown, PlacementVerbPriority, () => Add(targetStack, usedCard, user)),
            // Add the top card from the deck to the target stack
            usedDeck => verbs.Add(DeckPutDown,
                PlacementVerbPriority,
                () => TransferTopCard(usedDeck, targetStack, user)),
            // Pick a card from the used hand and add it to the target stack
            usedHand => verbs.Add(HandPutDown, PlacementVerbPriority, () => OpenPickerUi(usedHand, targetStack, user))
        );

        // Flip all: toggle, up, down.
        args.Verbs.Add(PlayingCardStackComponent.Verbs.Flip, () => FlipAll(targetStack, null, user));
        args.Verbs.Add(FlipUp, () => FlipAll(targetStack, false, user));
        args.Verbs.Add(FlipDown, () => FlipAll(targetStack, true, user));

        // Shuffle
        if (targetStack.Comp.NumCards > 1)
        {
            args.Verbs.Add(PlayingCardStackComponent.Verbs.Shuffle, () => Shuffle(targetStack, user));
        }
    }

    private void OnPlayingCardPicked<TStack>(Entity<TStack> entity, ref PlayingCardPickedEvent args)
        where TStack : PlayingCardStackComponent
    {
        if (args.Handled)
            return;

        Transfer(args.SourceHand, entity, args.Range, args.User);
        args.Handled = true;
    }

    /// Flips all cards in the given stack entity, handling audio, dirtying visuals, etc.
    private void FlipAll<T>(Entity<T> entity, bool? faceDown, EntityUid user) where T : PlayingCardStackComponent
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var didAnyFlip = entity.Comp switch
        {
            PlayingCardDeckComponent deck => deck.Cards.Aggregate(false,
                (current, card) => current | FlipCardInDeck(card)),
            PlayingCardHandComponent hand => hand.Cards.Aggregate(false,
                (current, card) => current | (NetEntToCard(card) is { } cardEnt && SetFacingOrFlip(cardEnt, faceDown))),
            _ => entity.Comp.ThrowUnknownInheritor<PlayingCardStackComponent, bool>(),
        };

        if (didAnyFlip)
        {
            entity.Comp.DirtyVisuals = true;
        }

        VerbAudioAndPopup(
            faceDown switch
            {
                true => FlipDown,
                false => FlipUp,
                null => PlayingCardStackComponent.Verbs.Flip,
            },
            entity,
            user
        );
    }

    /// Shuffles all cards in the given stack entity, handling audio, dirtying visuals, etc.
    private void Shuffle<T>(Entity<T> entity, EntityUid user) where T : PlayingCardStackComponent
    {
        var rand = new System.Random(
            SharedRandomExtensions.HashCodeCombine((int)_gameTiming.CurTick.Value, entity.Owner.Id));
        switch (entity.Comp)
        {
            case PlayingCardDeckComponent deck:
                rand.Shuffle(deck.Cards);
                break;
            case PlayingCardHandComponent hand:
                rand.Shuffle(hand.Cards);
                break;
            default:
                entity.Comp.ThrowUnknownInheritor<PlayingCardStackComponent>();
                break;
        }

        Dirty(entity);
        entity.Comp.DirtyVisuals = true;
        VerbAudioAndPopup(PlayingCardStackComponent.Verbs.Shuffle, user, entity);
    }

    /// Creates and returns an entity from <paramref name="protoId"/> at <paramref name="spawnAt"/>,
    /// <see cref="Add{TStack}(Entity{TStack}, IEnumerable{Entity{PlayingCardComponent}}, EntityCoordinates, EntityUid?)">adding</see>
    /// <paramref name="cards"/> to it in the order given.
    /// <seealso cref="CreateDeckPredicted"/>
    /// <seealso cref="CreateHandPredicted"/>
    private Entity<T> PredictedCreateStack<T>(
        EntProtoId<T> protoId,
        EntityCoordinates spawnAt,
        IEnumerable<Entity<PlayingCardComponent>> cards,
        EntityUid? user
    ) where T : PlayingCardStackComponent, new()
    {
        var ent = PredictedSpawnAtPosition(protoId, spawnAt);
        var stack = new Entity<T>(ent, Comp<T>(ent));
        Add(stack, cards, spawnAt, user);
        return stack;
    }


    /// Updates the visuals of any decks or hands with dirty visuals.
    public override void Update(float frameTime)
    {
        Update<PlayingCardDeckComponent>(deck => deck.Cards);
        Update<PlayingCardHandComponent>(hand => hand.Cards.Select(it => new PlayingCardInDeckNetEnt(it)));
    }

    private void Update<TStack>(Func<TStack, IEnumerable<PlayingCardInDeck>> cardAccessor)
        where TStack : PlayingCardStackComponent
    {
        // Iterate through all stacks of the given type.
        var stacks = EntityQueryEnumerator<TStack>();
        while (stacks.MoveNext(out var ent, out var comp))
        {
            // If they're not dirty, or they're gonna be deleted, skip them.
            if (!comp.DirtyVisuals ||
                Deleted(ent))
                continue;

            // Set the visible cards based on the cards currently in the stack.
            _appearance.SetData(
                ent,
                PlayingCardStackVisuals.Cards,
                cardAccessor(comp).Take(^Math.Min(comp.VisualLimit, comp.NumCards)..).ToList()
            );

            // No longer dirty.
            comp.DirtyVisuals = false;
        }
    }
}

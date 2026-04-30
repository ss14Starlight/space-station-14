using Content.Shared._Moffstation.Cards.Systems;
using Content.Shared._Moffstation.Extensions;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Cards.Components;

/// This abstract class contains fields shared by <see cref="PlayingCardDeckComponent"/> and
/// <see cref="PlayingCardHandComponent"/>.
[Access(typeof(SharedPlayingCardsSystem))]
public abstract partial class PlayingCardStackComponent : Component, ISealedInheritance
{
    [DataField]
    public SoundSpecifier PickUpSound = new SoundCollectionSpecifier("cardSlide");

    [DataField]
    public SoundSpecifier PlaceDownSound = new SoundCollectionSpecifier("cardShove");

    /// The ID of <see cref="Container"/>.
    [DataField]
    public string ContainerId = "playing-card-stack-container";

    /// The container which holds the card entities in this stack.
    [ViewVariables]
    public Container Container = default!;

    /// The number of cards in this stack.
    [ViewVariables]
    public abstract int NumCards { get; }

    /// This field indicates whether the visuals of this stack need to be updated. This is used to avoid repeatedly
    /// updating visuals on the same stack in a single frame.
    /// <see cref="SharedPlayingCardsSystem.Update"/>
    public bool DirtyVisuals = true;

    /// The maximum number of cards which will be included in the visuals of this stack.
    [DataField(required: true)]
    public int VisualLimit;

    public static readonly LocId ExamineText = "playing-card-stack-examine";

    public static class Verbs
    {
        public static readonly VerbInfo CardPutDown = VerbInfo.Build("playing-card-stack-card-put-down", icon: "drop");
        public static readonly VerbInfo DeckPutDown = VerbInfo.Build("playing-card-stack-deck-put-down", icon: "drop");
        public static readonly VerbInfo HandPutDown = VerbInfo.Build("playing-card-stack-hand-put-down", icon: "drop");

        public static readonly VerbInfo Flip = VerbInfo.Build("playing-card-flip-all", icon: "flip", sounds: "cardFan");
        public static readonly VerbInfo FlipUp = VerbInfo.Build("playing-card-flip-all-up", icon: "flip", sounds: "cardFan");
        public static readonly VerbInfo FlipDown = VerbInfo.Build("playing-card-flip-all-down", icon: "flip", sounds: "cardFan");
        public static readonly VerbInfo Shuffle = VerbInfo.Build("playing-card-shuffle", icon: "die", sounds: "cardFan");
    }
}

[Serializable, NetSerializable]
public enum PlayingCardStackVisuals : byte
{
    /// This key for appearance data indicates which cards are visible in a stack. It is expected to key a value of type
    /// <c>List&lt;<see cref="PlayingCardInDeck"/>&gt;</c>.
    Cards,
}

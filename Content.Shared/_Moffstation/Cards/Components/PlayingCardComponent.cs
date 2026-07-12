using Content.Shared._Moffstation.Cards.Systems;
using Content.Shared._Moffstation.Extensions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Cards.Components;

/// A playing card which can be flipped, inserted into a hand, or joined into a deck.
/// <seealso cref="SharedPlayingCardsSystem"/>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedPlayingCardsSystem))]
public sealed partial class PlayingCardComponent : Component
{
    /// The sprite layers of the face, or front, of the card.
    [DataField(required: true), AutoNetworkedField]
    public PrototypeLayerData[] ObverseLayers;

    /// The sprite layers of the back of the card.
    [DataField(required: true), AutoNetworkedField]
    public PrototypeLayerData[] ReverseLayers;

    /// The current sprite layers (based on <see cref="FaceDown"/>), or the sprite layers for the given
    /// <paramref name="faceDownOverride"/>.
    [Access(Other = AccessPermissions.ReadExecute)] // Pure function, I don't care if you execute it.
    public PrototypeLayerData[] Sprite(bool? faceDownOverride = null) => faceDownOverride ?? FaceDown
        ? ReverseLayers
        : ObverseLayers;

    /// Sprite layers added to this entity based on <see cref="Sprite"/>.
    /// This is used by the client visualizer system to correctly remove added layers when the card is flipped.
    [ViewVariables]
    public HashSet<string> SpriteLayersAdded = [];

    /// Is the card facing down, ie. which side is visible. If true, the <see cref="ReverseLayers"/> is visible.
    [DataField, AutoNetworkedField]
    public bool FaceDown;

    /// The name of this card, visible when not face down.
    [DataField("name", required: true), AutoNetworkedField]
    public string ObverseName;

    /// The description of this card, visible when not face down.
    [DataField(required: true), AutoNetworkedField]
    public string Description;

    /// The name which will be applied to this entity when it is flipped face down.
    [DataField(required: true), AutoNetworkedField]
    public string ReverseName;

    /// The description which will be applied to this entity when it is flipped face down.
    [DataField, AutoNetworkedField]
    public string? ReverseDescription;

    /// The current sprite layers (based on <see cref="FaceDown"/>), or the sprite layers for the given
    /// <paramref name="faceDownOverride"/>.
    [Access(Other = AccessPermissions.ReadExecute)] // Pure function, I don't care if you execute it.
    public string Name(bool? faceDownOverride = null) => faceDownOverride ?? FaceDown
        ? ReverseName
        : ObverseName;

    public static readonly LocId ExamineText = "playing-card-examine";

    public static class Verbs
    {
        public static readonly VerbInfo CardPickup = VerbInfo.Build("playing-card-card-card-pickup", icon: "pickup");
        public static readonly VerbInfo StackPickup = VerbInfo.Build("playing-card-card-stack-pickup", icon: "pickup");

        public static readonly VerbInfo CardPutDown = VerbInfo.Build("playing-card-card-card-put-down", icon: "drop");
        public static readonly VerbInfo DeckPutDown = VerbInfo.Build("playing-card-card-deck-put-down", icon: "drop");
        public static readonly VerbInfo HandPutDown = VerbInfo.Build("playing-card-card-hand-put-down", icon: "drop");

        public static readonly VerbInfo Flip = VerbInfo.Build("playing-card-flip", icon: "flip");
    }
}

/// The key used to access appearance data for <see cref="PlayingCardComponent"/>.
/// <seealso cref="AppearanceComponent"/>
[Serializable, NetSerializable]
public enum PlayingCardVisuals : sbyte
{
    IsFaceDown,
}

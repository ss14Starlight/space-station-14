using Content.Shared.Chat.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Daiquiri.Emoting;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class AnimationFlipEmoteEvent : EntityEventArgs
{
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class AnimationSpinEmoteEvent : EntityEventArgs
{
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class AnimationJumpEmoteEvent : EntityEventArgs
{
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class AnimationDanceEmoteEvent : EntityEventArgs
{
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AnimatedEmotesComponent : Component
{
    [DataField]
    public ProtoId<EmotePrototype>? Emote;

    [ViewVariables]
    public int Sequence;

    [ViewVariables]
    public int LastSequence;
}

[Serializable, NetSerializable]
public sealed partial class AnimatedEmotesComponentState : ComponentState
{
    public ProtoId<EmotePrototype>? Emote;
    public int Sequence;

    public AnimatedEmotesComponentState(ProtoId<EmotePrototype>? emote, int sequence)
    {
        Emote = emote;
        Sequence = sequence;
    }
}

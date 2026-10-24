using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Changeling;

[Serializable, NetSerializable]
public sealed class AcceptChangelingHiveChoiceMessage(bool accepted) : EuiMessageBase
{
    public readonly bool Accepted = accepted;
}

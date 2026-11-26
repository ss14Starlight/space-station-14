using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared._Starlight.Tools.Towing;

[Serializable, NetSerializable]
public sealed partial class UnTieDoAfter : DoAfterEvent
{
    public readonly string JointId;

    public UnTieDoAfter(string jointId) => JointId = jointId;

    public override DoAfterEvent Clone() => new UnTieDoAfter(JointId);
}

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons.Borgs;

[Serializable, NetSerializable]
public enum AcceptBorgingUiButton
{
    Deny,
    Accept,
}

[Serializable, NetSerializable]
public sealed class AcceptBorgingChoiceMessage(AcceptBorgingUiButton button) : EuiMessageBase
{
    public readonly AcceptBorgingUiButton Button = button;
}

[Serializable, NetSerializable]
public sealed class AskBorgingChoiceEvent();

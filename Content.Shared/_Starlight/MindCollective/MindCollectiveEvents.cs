using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.MindCollective;

/// <summary>Raised on the collective after a mind accepts membership.</summary>
public sealed class MindCollectiveJoinedEvent(EntityUid mind, EntityUid? previousBody) : EntityEventArgs
{
    public readonly EntityUid Mind = mind;
    public readonly EntityUid? PreviousBody = previousBody;
}

/// <summary>Raised on the collective after a member leaves. Other systems own their roles and abilities.</summary>
public sealed class MindCollectiveLeftEvent(EntityUid mind, EntityUid? previousBody) : EntityEventArgs
{
    public readonly EntityUid Mind = mind;
    public readonly EntityUid? PreviousBody = previousBody;
}

/// <summary>Raised on a registered body after the collective transfers control.</summary>
public sealed class MindCollectiveBodyControlChangedEvent(EntityUid? previousMind, EntityUid? mind) : EntityEventArgs
{
    public readonly EntityUid? PreviousMind = previousMind;
    public readonly EntityUid? Mind = mind;
}

[Serializable, NetSerializable]
public sealed class MindCollectiveConsentState(string name, string title, string description) : EuiStateBase
{
    public readonly string Name = name;
    public readonly string Title = title;
    public readonly string Description = description;
}

[Serializable, NetSerializable]
public sealed class MindCollectiveConsentMessage(bool accepted) : EuiMessageBase
{
    public readonly bool Accepted = accepted;
}

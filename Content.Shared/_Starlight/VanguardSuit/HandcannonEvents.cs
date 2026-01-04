using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.VanguardSuit;

/// <summary>
/// Event fired when the handcannon deployment action is used.
/// </summary>
public sealed partial class DeployHandcannonActionEvent : InstantActionEvent
{
}

/// <summary>
/// DoAfter event for handcannon deployment.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class HandcannonDeployDoAfterEvent : SimpleDoAfterEvent
{
}

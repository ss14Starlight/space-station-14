using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Admeme.Jumpscare;

public sealed partial class JumpscareActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class MassJumpscareDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class DoJumpscareEvent(JumpscareData jumpscare, bool ignoreDelay = false) : EntityEventArgs
{
    public readonly JumpscareData Jumpscare = jumpscare;

    /// <summary>
    /// Play immediately, instead of waiting for <see cref="JumpscareData.StartDelay"/>.
    /// </summary>
    public readonly bool IgnoreDelay = ignoreDelay;
}

[Serializable, NetSerializable]
public sealed class JumpscareTargetMessage(List<NetEntity> targets, JumpscareData jumpscare) : EuiMessageBase
{
    /// <summary>
    /// Targets picked on the client. Ignored by the server if the panel has a locked target.
    /// </summary>
    public readonly List<NetEntity> Targets = targets;
    public readonly JumpscareData Jumpscare = jumpscare;
}

/// <summary>
/// Asks the server to resend the player list for the jumpscare panel.
/// </summary>
[Serializable, NetSerializable]
public sealed class JumpscareRefreshPlayersMessage : EuiMessageBase;

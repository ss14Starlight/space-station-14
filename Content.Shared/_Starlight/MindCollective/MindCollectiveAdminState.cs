using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.MindCollective;

[Serializable, NetSerializable]
public sealed record MindCollectiveAdminEntity(NetEntity Entity, string Name);

[Serializable, NetSerializable]
public sealed record MindCollectiveAdminPlayer(NetUserId User, string Name);

[Serializable, NetSerializable]
public sealed record MindCollectiveAdminRole(string Id, string Name, bool Assigned);

[Serializable, NetSerializable]
public sealed class MindCollectiveAdminState(
    List<MindCollectiveAdminEntity> collectives,
    List<MindCollectiveAdminPlayer> players,
    List<MindCollectiveAdminEntity> bodies,
    List<MindCollectiveAdminRole> roles,
    NetEntity? collective,
    NetUserId? player,
    NetEntity? body,
    string feedback) : EuiStateBase
{
    public readonly List<MindCollectiveAdminEntity> Collectives = collectives;
    public readonly List<MindCollectiveAdminPlayer> Players = players;
    public readonly List<MindCollectiveAdminEntity> Bodies = bodies;
    public readonly List<MindCollectiveAdminRole> Roles = roles;
    public readonly NetEntity? Collective = collective;
    public readonly NetUserId? Player = player;
    public readonly NetEntity? Body = body;
    public readonly string Feedback = feedback;
}

[Serializable, NetSerializable]
public enum MindCollectiveAdminAction : byte
{
    Refresh,
    Create,
    Invite,
    AddBody,
    RemoveBody,
    SpawnBody,
    GrantRole,
    RevokeRole,
    Enter,
    Observe,
    Control,
    Leave,
}

[Serializable, NetSerializable]
public sealed class MindCollectiveAdminMessage(
    MindCollectiveAdminAction action,
    NetEntity? collective,
    NetUserId? player,
    NetEntity? body,
    string? role) : EuiMessageBase
{
    public readonly MindCollectiveAdminAction Action = action;
    public readonly NetEntity? Collective = collective;
    public readonly NetUserId? Player = player;
    public readonly NetEntity? Body = body;
    public readonly string? Role = role;
}

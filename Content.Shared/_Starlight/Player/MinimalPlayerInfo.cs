using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Player;

/// <summary>
/// For associating GUID with username
/// </summary>
[Serializable]
[NetSerializable]
public sealed class MinimalPlayerInfo(string username, NetUserId userId)
{
    public string Username = username;
    public NetUserId UserId = userId;
}

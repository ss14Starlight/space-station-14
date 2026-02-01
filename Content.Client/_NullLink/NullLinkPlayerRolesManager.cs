using System.Linq;
using Content.Client.Administration.Managers;
using Content.Shared._NullLink;
using Robust.Shared.Network;

namespace Content.Client._NullLink;

public sealed class NullLinkPlayerRolesManager : INullLinkPlayerRolesManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private HashSet<ulong> _roles = [];
    private string? _discordLink;
    private ISawmill _sawmill = default!;

    public event Action PlayerRolesChanged = delegate { };

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("admin");
        _netMgr.RegisterNetMessage<MsgUpdatePlayerRoles>(Update);
    }

    private void Update(MsgUpdatePlayerRoles message)
    {
        _roles = message.Roles;
        _discordLink = message.DiscordLink;

        _sawmill.Info("Updated player roles");
        PlayerRolesChanged?.Invoke();
    }

    public string? GetDiscordLink()
        => _discordLink;

    public bool ContainsAny(ulong[] roles)
        => roles.Any(_roles.Contains);
}

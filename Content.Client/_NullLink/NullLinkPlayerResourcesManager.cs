using System.Diagnostics.CodeAnalysis;
using Content.Shared._NullLink;
using Robust.Client.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client._NullLink;

public sealed partial class NullLinkPlayerResourcesManager : SharedNullLinkPlayerResourcesManager, INullLinkPlayerResourcesManager
{
    [Dependency] private IClientNetManager _netMgr = default!;
    [Dependency] private IPlayerManager _player = default!;

    private Dictionary<string, double> _playerResources = [];
    private bool _receivedInitial;

    public event Action PlayerResourcesChanged = delegate { };


    public override void Initialize()
    {
        base.Initialize();
        _netMgr.RegisterNetMessage<MsgUpdatePlayerResources>(Update);
    }

    private void Update(MsgUpdatePlayerResources message)
    {
        _playerResources = new Dictionary<string, double>(message.Resources);
        _receivedInitial = true;

        if (_player.LocalSession is { } session)
            ReplaceLocalResources(session.UserId, _playerResources);

        _sawmill.Info("Updated player resources");
        PlayerResourcesChanged?.Invoke();
    }

    public bool TryGetResources([NotNullWhen(true)] out Dictionary<string, double>? value)
    {
        value = null;
        if (!_receivedInitial)
            return false;

        value = new Dictionary<string, double>(_playerResources);
        return true;
    }

    public bool TryGetResource(string id, [NotNullWhen(true)] out double? value)
    {
        value = null;
        if (!_receivedInitial || !_playerResources.TryGetValue(id, out var stored))
            return false;

        value = stored;
        return true;
    }

    public override bool TryGetResources(ICommonSession session, [NotNullWhen(true)] out Dictionary<string, double>? value)
    {
        if (_player.LocalUser == session.UserId)
            return TryGetResources(out value);

        return base.TryGetResources(session, out value);
    }

    public override bool TryGetResource(ICommonSession session, string id, [NotNullWhen(true)] out double? value)
    {
        if (_player.LocalUser == session.UserId)
            return TryGetResource(id, out value);

        return base.TryGetResource(session, id, out value);
    }
}

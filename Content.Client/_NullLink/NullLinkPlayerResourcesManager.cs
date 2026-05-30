using System.Diagnostics.CodeAnalysis;
using Content.Shared._NullLink;
using Robust.Shared.Network;

namespace Content.Client._NullLink;

public sealed class NullLinkPlayerResourcesManager : SharedNullLinkPlayerResourcesManager, INullLinkPlayerResourcesManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    private Dictionary<string, double> _playerResources = [];

    public event Action PlayerResourcesChanged = delegate { };


    public override void Initialize()
    {
        base.Initialize();
        _netMgr.RegisterNetMessage<MsgUpdatePlayerResources>(Update);
    }

    private void Update(MsgUpdatePlayerResources message)
    {
        _playerResources = message.Resources;

        _sawmill.Info("Updated player resources");
        PlayerResourcesChanged?.Invoke();
    }

    public bool TryGetResources([NotNullWhen(true)] out Dictionary<string, double>? value)
    {
        value = null;
        if (_playerResources.Count <= 0)
            return false;

        value = _playerResources;
        return true;
    }

    public bool TryGetResource(string id, [NotNullWhen(true)] out double? value)
    {
        value = null;
        if (!_playerResources.TryGetValue(id, out var Value))
            return false;

        value = Value;
        return true;
    }
}

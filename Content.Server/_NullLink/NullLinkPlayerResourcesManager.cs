using Content.Server._NullLink.Core;
using Content.Shared._NullLink;
using Content.Shared.NullLink.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._NullLink;

public sealed partial class NullLinkPlayerResourcesManager : SharedNullLinkPlayerResourcesManager
{
    [Dependency] private IActorRouter _actors = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerNetManager _netMgr = default!;

    private bool _resourcesEnabled;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(NullLinkCCVars.ResourcesEnabled, UpdateResources, true);
    }

    private void UpdateResources(bool obj)
        => _resourcesEnabled = obj;

    public override bool TryUpdateResource(ICommonSession session, string id, double value, bool skipNullLink = false)
    {
        if (!base.TryUpdateResource(session, id, value, skipNullLink))
            return false;

        return true;
    }

    public override bool TrySetResource(ICommonSession session, string id, double value, bool skipNullLink = false)
    {
        if (!base.TrySetResource(session, id, value, skipNullLink))
            return false;

        return true;
    }

    protected override void OnResourceChanged(
        ICommonSession session,
        string id,
        double oldValue,
        double newValue,
        bool skipNullLink)
    {
        SendPlayerResources(session);

        var diff = newValue - oldValue;
        _sawmill.Debug($"Updated resource {id} OLD: {oldValue} NEW: {newValue} DIFF: {diff}");

        if (!_resourcesEnabled
            || skipNullLink
            || diff == 0
            || !_actors.Enabled
            || !_actors.TryGetServerGrain(out var serverGrain))
            return;

        serverGrain.UpdateResource(session.UserId, id, diff);
    }

    protected override void OnResourcesReplaced(ICommonSession session, Dictionary<string, double> resources)
    {
        // Bulk replacement is used for inbound NullLink sync; notify the client but do not echo.
        SendPlayerResources(session);
    }

    private void SendPlayerResources(ICommonSession session)
    {
        if (!TryGetResources(session, out var resources))
            return;

        _netMgr.ServerSendMessage(new MsgUpdatePlayerResources
        {
            Resources = resources
        }, session.Channel);
    }
}

using Content.Server._NullLink.Core;
using Content.Shared._NullLink;
using Content.Shared.NullLink.CCVar;
using Content.Shared.Starlight;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._NullLink;

public sealed class NullLinkPlayerResourcesManager : SharedNullLinkPlayerResourcesManager
{
    [Dependency] private readonly ISharedPlayersRoleManager _sharedPlayers = default!;
    [Dependency] private readonly IActorRouter _actors = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _resourcesEnabled = false;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(NullLinkCCVars.ResourcesEnabled, UpdateResources, true);
    }

    private void UpdateResources(bool obj) 
        => _resourcesEnabled = obj;

    public override bool TryUpdateResource(ICommonSession session, string id, double value, bool skipNullLink = false)
    {

        if (_sharedPlayers.GetPlayerData(session) is not { } data 
            || value == 0) // If we don't have any difference - we don't need to call null link.
            return false;

        double oldValue = 0;
        if (data.Resources.TryGetValue(id, out var current))
        {
            data.Resources[id] = current + value;
            oldValue = current + value;
        }
        else
            data.Resources[id] = value;

        if (!_resourcesEnabled
            || skipNullLink
            || !_actors.Enabled
            || !_actors.TryGetServerGrain(out var serverGrain))
            return true;

        serverGrain.UpdateResource(session.UserId, "credits", value);
        _sawmill.Debug($"Updated balance OLD: {oldValue} NEW: {data.Resources[id]} DIFF: {value}");

        return true;
    }

    public override bool TrySetResource(ICommonSession session, string id, double value, bool skipNullLink = false)
    {
        if (_sharedPlayers.GetPlayerData(session) is not { } data)
            return false;

        var oldValue = data.Resources[id];

        var diff = value - oldValue;

        if (diff == 0) // If we don't have any difference - we don't need to call null link.
            return false;

        data.Resources[id] = value;

        if (!_resourcesEnabled 
            || skipNullLink
            || !_actors.Enabled
            || !_actors.TryGetServerGrain(out var serverGrain) )
            return true;

        serverGrain.UpdateResource(session.UserId, "credits", diff);
        _sawmill.Debug($"Updated balance OLD: {oldValue} NEW: {data.Resources[id]} DIFF: {diff}");

        return true;
    }
}

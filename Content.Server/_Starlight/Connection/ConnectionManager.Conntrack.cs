using System.Net;
using Content.Shared._Starlight.CCVar;
using Robust.Shared;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Connection;

public sealed partial class ConnectionManager
{
    private const int ConntrackLidgrenIpLimit = 1_000_000;

    private void InitializeConntrackNetLimits()
    {
        _cfg.OnValueChanged(StarlightCCVars.ConntrackEnabled, enabled =>
        {
            if (!enabled)
                return;

            // Only the defaults are overridden, so explicit values in the server config still win.
            _cfg.OverrideDefault(CVars.NetMaxRapidConnections, ConntrackLidgrenIpLimit);
            _cfg.OverrideDefault(CVars.NetMaxIpConnections, ConntrackLidgrenIpLimit);
        }, true);
    }

    public IPAddress? GetPlayerAddress(ICommonSession session)
    {
        if (GetResolvedAddress(session.UserId) is { } resolved)
            return resolved;

        var address = session.Channel.RemoteEndPoint.Address;
        // Never hand out the node address: it is shared by every player behind that node.
        return _conntrack.IsSnatAddress(address) ? null : address;
    }

    private void ForgetResolvedAddress(ICommonSession session)
    {
        if (_resolvedAddresses.TryGetValue(session.UserId, out var entry)
            && entry.Endpoint.Equals(session.Channel.RemoteEndPoint))
        {
            _resolvedAddresses.Remove(session.UserId);
        }
    }
}

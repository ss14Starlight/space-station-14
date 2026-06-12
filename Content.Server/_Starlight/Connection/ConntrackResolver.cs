using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._Starlight.Connection;

public enum ConntrackStatus
{
    NotApplicable,
    Resolved,
    Failed,
}

/// <summary>
/// Resolves the real client IP via a conntrack-agent running on the Kubernetes node.
/// When kube-proxy performs SNAT (NodePort), the game server sees the node IP instead
/// of the real client IP. The conntrack-agent queries the kernel conntrack table using
/// the masquerade source port and returns the original client IP.
/// Each Kubernetes node runs a conntrack-agent on the same port. The resolver checks
/// whether the source IP falls within any of the configured node subnets and, if so,
/// queries that node's agent at <c>http://{srcIp}:{port}/conntrack?port={masqPort}</c>.
/// Supports both IPv4 and IPv6 (dual-stack) subnets.
/// </summary>
public sealed class ConntrackResolver
{
    private readonly IHttpClientHolder _http;
    private readonly ISawmill _sawmill;

    private bool _enabled;
    private int _port;
    private List<(IPAddress Network, int PrefixLength)> _subnets = [];

    public ConntrackResolver(IHttpClientHolder http, IConfigurationManager cfg, ILogManager logManager)
    {
        _http = http;
        _sawmill = logManager.GetSawmill("conntrack");

        cfg.OnValueChanged(StarlightCCVars.ConntrackEnabled, v => _enabled = v, true);
        cfg.OnValueChanged(StarlightCCVars.ConntrackPort, v => _port = v, true);
        cfg.OnValueChanged(StarlightCCVars.ConntrackSubnet, ParseSubnets, true);
    }

    private void ParseSubnets(string raw)
    {
        var list = new List<(IPAddress, int)>();

        if (string.IsNullOrWhiteSpace(raw))
        {
            _subnets = list;
            return;
        }

        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('/');
            if (parts.Length != 2
                || !IPAddress.TryParse(parts[0], out var network)
                || !int.TryParse(parts[1], out var prefix)
                || prefix < 0
                || (network.AddressFamily == AddressFamily.InterNetwork && prefix > 32)
                || (network.AddressFamily == AddressFamily.InterNetworkV6 && prefix > 128))
            {
                _sawmill.Warning("Ignoring invalid CIDR in conntrack.subnet: {Value}", entry);
                continue;
            }

            list.Add((network, prefix));
        }

        _subnets = list;
    }

    public bool IsSnatAddress(IPAddress address) => IsInAnySubnet(address);

    private bool IsInAnySubnet(IPAddress address)
    {
        foreach (var (network, prefixLength) in _subnets)
        {
            if (IsInSubnet(address, network, prefixLength))
                return true;
        }

        return false;
    }

    private static bool IsInSubnet(IPAddress address, IPAddress network, int prefixLength)
    {
        var addr = address;

        // Handle IPv4-mapped IPv6 (e.g. ::ffff:10.42.0.1) against IPv4 subnets.
        if (addr.AddressFamily == AddressFamily.InterNetworkV6
            && addr.IsIPv4MappedToIPv6
            && network.AddressFamily == AddressFamily.InterNetwork)
        {
            addr = addr.MapToIPv4();
        }

        if (addr.AddressFamily != network.AddressFamily)
            return false;

        var addrBytes = addr.GetAddressBytes();
        var netBytes = network.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (addrBytes[i] != netBytes[i])
                return false;
        }

        if (remainingBits > 0)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((addrBytes[fullBytes] & mask) != (netBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }

    private static string FormatHostForUrl(IPAddress address)
        => address.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{address}]"
            : address.ToString();

    /// <summary>
    /// Resolves the real client IP via the conntrack-agent.
    /// If the source address falls within any configured node subnet, queries
    /// <c>http://{srcIp}:{port}/conntrack?port={masqPort}</c> (with brackets for IPv6).
    /// </summary>
    public async Task<(ConntrackStatus Status, IPAddress? Ip)> ResolveRealIp(IPEndPoint masqueradeEndpoint)
    {
        if (!_enabled || _subnets.Count == 0)
            return (ConntrackStatus.NotApplicable, null);

        if (!IsInAnySubnet(masqueradeEndpoint.Address))
            return (ConntrackStatus.NotApplicable, null);

        // Past this point the source IS a node/masquerade address, so any failure below is a real failure:
        // returning the node IP would be wrong, so we report Failed and let the caller fall back.
        try
        {
            var host = FormatHostForUrl(masqueradeEndpoint.Address);
            var url = $"http://{host}:{_port}/conntrack?port={masqueradeEndpoint.Port}";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var response = await _http.Client.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _sawmill.Warning("Conntrack agent on {Node} returned HTTP {StatusCode}",
                    masqueradeEndpoint.Address, response.StatusCode);
                return (ConntrackStatus.Failed, null);
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            var result = JsonSerializer.Deserialize<ConntrackResponse>(json);

            if (result is null)
            {
                _sawmill.Warning("Conntrack agent on {Node} returned unparseable response",
                    masqueradeEndpoint.Address);
                return (ConntrackStatus.Failed, null);
            }

            if (result.Error is not null)
            {
                _sawmill.Warning("Conntrack agent on {Node} error: {Error}",
                    masqueradeEndpoint.Address, result.Error);
                return (ConntrackStatus.Failed, null);
            }

            if (result.Ip is null)
            {
                _sawmill.Warning("Conntrack agent on {Node} found no entry for masquerade port {Port}",
                    masqueradeEndpoint.Address, masqueradeEndpoint.Port);
                return (ConntrackStatus.Failed, null);
            }

            if (IPAddress.TryParse(result.Ip, out var realIp))
            {
                _sawmill.Verbose("Resolved real IP for masquerade port {Port} via node {Node}",
                    masqueradeEndpoint.Port, masqueradeEndpoint.Address);
                return (ConntrackStatus.Resolved, realIp);
            }

            _sawmill.Warning("Conntrack agent on {Node} returned invalid IP format",
                masqueradeEndpoint.Address);
            return (ConntrackStatus.Failed, null);
        }
        catch (OperationCanceledException)
        {
            _sawmill.Warning("Conntrack resolution timed out for port {Port} via node {Node}",
                masqueradeEndpoint.Port, masqueradeEndpoint.Address);
            return (ConntrackStatus.Failed, null);
        }
        catch (Exception ex)
        {
            _sawmill.Warning("Conntrack resolution via node {Node} failed: {Error}",
                masqueradeEndpoint.Address, ex.Message);
            return (ConntrackStatus.Failed, null);
        }
    }

    private sealed class ConntrackResponse
    {
        [JsonPropertyName("ip")]
        public string? Ip { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}

using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Whether conntrack-based real IP resolution is enabled.
    /// When running behind a NodePort kube-proxy with SNAT, the game server sees the node IP
    /// instead of the real client IP. The conntrack-agent resolves the real IP from the kernel
    /// conntrack table using the masquerade port.
    /// </summary>
    public static readonly CVarDef<bool> ConntrackEnabled =
        CVarDef.Create("conntrack.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// Port on which the conntrack-agent HTTP service listens on every Kubernetes node (e.g. 9876).
    /// </summary>
    public static readonly CVarDef<int> ConntrackPort =
        CVarDef.Create("conntrack.port", 9876, CVar.SERVERONLY);

    /// <summary>
    /// Comma-separated list of Kubernetes node subnets in CIDR notation
    /// (e.g. "10.42.0.0/16" or "10.42.0.0/16,fd42::/64" for dual-stack).
    /// When a connection arrives from an IP within any of these subnets, SNAT is assumed
    /// and the conntrack-agent on that node is queried to resolve the real client IP.
    /// </summary>
    public static readonly CVarDef<string> ConntrackSubnet =
        CVarDef.Create("conntrack.subnet", "10.42.0.0/16", CVar.SERVERONLY | CVar.CONFIDENTIAL);
}

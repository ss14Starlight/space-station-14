using System.Collections.Generic;
using System.Net;
using Content.Shared._Starlight.CCVar;

// ReSharper disable once CheckNamespace
namespace Content.Server.Connection;

public sealed partial class ConnectionManager
{
    private readonly object _rateLock = new();
    private readonly Dictionary<IPAddress, Queue<TimeSpan>> _rateByIp = new();
    private readonly Queue<TimeSpan> _rateGlobal = new();
    private TimeSpan _lastRatePrune;

    private string? GlobalRateLimitDeny()
    {
        if (!_cfg.GetCVar(StarlightCCVars.ConnRateLimitEnabled))
            return null;

        var now = _gameTiming.RealTime;
        lock (_rateLock)
        {
            var window = TimeSpan.FromSeconds(_cfg.GetCVar(StarlightCCVars.ConnRateLimitGlobalSeconds));
            Trim(_rateGlobal, now - window);
            if (_rateGlobal.Count >= _cfg.GetCVar(StarlightCCVars.ConnRateLimitGlobalCount))
            {
                _sawmill.Warning($"connection rate-limited (global): {_rateGlobal.Count} attempts in {window.TotalSeconds}s");
                return Loc.GetString("starlight-conn-ratelimited");
            }

            _rateGlobal.Enqueue(now);
            return null;
        }
    }

    private string? PerIpRateLimitDeny(IPAddress addr)
    {
        if (!_cfg.GetCVar(StarlightCCVars.ConnRateLimitEnabled))
            return null;

        var now = _gameTiming.RealTime;
        lock (_rateLock)
        {
            var window = TimeSpan.FromSeconds(_cfg.GetCVar(StarlightCCVars.ConnRateLimitPerIpSeconds));
            if (!_rateByIp.TryGetValue(addr, out var hits))
                _rateByIp[addr] = hits = new Queue<TimeSpan>();

            Trim(hits, now - window);
            if (hits.Count >= _cfg.GetCVar(StarlightCCVars.ConnRateLimitPerIpCount))
            {
                _sawmill.Warning($"connection rate-limited (per-ip) {addr}: {hits.Count} attempts in {window.TotalSeconds}s");
                return Loc.GetString("starlight-conn-ratelimited");
            }

            hits.Enqueue(now);
            PruneIdle(now);
            return null;
        }
    }

    private static void Trim(Queue<TimeSpan> q, TimeSpan cutoff)
    {
        while (q.Count > 0 && q.Peek() < cutoff)
            q.Dequeue();
    }

    private void PruneIdle(TimeSpan now)
    {
        if (now - _lastRatePrune < TimeSpan.FromSeconds(30))
            return;
        _lastRatePrune = now;

        var cutoff = now - TimeSpan.FromSeconds(_cfg.GetCVar(StarlightCCVars.ConnRateLimitPerIpSeconds));
        List<IPAddress>? stale = null;
        foreach (var (ip, q) in _rateByIp)
        {
            Trim(q, cutoff);
            if (q.Count == 0)
                (stale ??= []).Add(ip);
        }
        if (stale != null)
        {
            foreach (var ip in stale)
                _rateByIp.Remove(ip);
        }
    }
}

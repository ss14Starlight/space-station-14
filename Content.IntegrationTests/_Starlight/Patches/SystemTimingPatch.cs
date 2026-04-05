using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Content.IntegrationTests._Starlight.Patches;

/// <summary>
///     Reads per-system tick timing from the Prometheus histogram that EntitySystemManager already records
///     (when MetricsEnabled = true) and prints a top-10 summary after each test to TestOut.
/// </summary>
internal static class SystemTimingPatch
{
    private static readonly MethodInfo _exportMethod = ResolveExportMethod();

    private static Dictionary<string, double> s_snapshot = [];

    internal static void EnableMetrics(Robust.Shared.GameObjects.IEntitySystemManager sysMan)
        => sysMan.MetricsEnabled = true;

    internal static async Task TakeSnapshot()
        => s_snapshot = await CollectSums();

    internal static async Task PrintTop10(TextWriter output)
    {
        var current = await CollectSums();
        if (current.Count == 0)
            return;

        var deltas = current
            .Select(kv => (Name: kv.Key, Delta: kv.Value - s_snapshot.GetValueOrDefault(kv.Key)))
            .Where(x => x.Delta > 1e-6)
            .OrderByDescending(x => x.Delta)
            .Take(10)
            .ToList();

        if (deltas.Count == 0)
            return;

        await output.WriteLineAsync("  ┌─ Top 10 systems by tick time");
        for (var i = 0; i < deltas.Count; i++)
            await output.WriteLineAsync($"  │ {i + 1,2}. {deltas[i].Name,-55} {deltas[i].Delta * 1000,8:F2} ms");
        await output.WriteLineAsync("  └" + new string('─', 70));
    }

    private static async Task<Dictionary<string, double>> CollectSums()
    {
        if (_exportMethod == null)
            return [];

        var registry = ResolveDefaultRegistry();
        if (registry == null)
            return [];

        using var mem = new MemoryStream();
        var task = (Task?)_exportMethod.Invoke(registry, [mem, CancellationToken.None]);
        if (task != null)
            await task;

        return ParseSums(Encoding.UTF8.GetString(mem.ToArray()));
    }

    private static Dictionary<string, double> ParseSums(string text)
    {
        var result = new Dictionary<string, double>();
        const string Prefix = "robust_entity_systems_update_usage_sum{system=\"";

        foreach (var line in text.AsSpan().EnumerateLines())
        {
            if (!line.StartsWith(Prefix))
                continue;

            var afterPrefix = line[Prefix.Length..];
            var nameEnd = afterPrefix.IndexOf('"');
            if (nameEnd < 0)
                continue;

            var name = afterPrefix[..nameEnd].ToString();

            var valPart = afterPrefix[(nameEnd + 3)..];
            var spaceIdx = valPart.IndexOf(' ');
            var valSpan = spaceIdx >= 0 ? valPart[..spaceIdx] : valPart;

            if (double.TryParse(valSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                result[name] = val;
        }

        return result;
    }

    private static MethodInfo ResolveExportMethod()
    {
        var registry = ResolveDefaultRegistry();
        if (registry == null)
            return null;

        return registry.GetType().GetMethod(
            "CollectAndExportAsTextAsync",
            [typeof(Stream), typeof(CancellationToken)]);
    }

    private static object ResolveDefaultRegistry()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(static a => a.GetName().Name == "Prometheus.NetStandard");

        return asm?.GetType("Prometheus.Metrics")
            ?.GetProperty("DefaultRegistry", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);
    }
}

using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Log;

namespace Content.Client._Starlight.Logs;

/// <summary>
/// Restores persisted sawmill log levels from CVar on startup.
/// </summary>
public sealed class LogLevelSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        ApplySavedLevels();
    }

    private void ApplySavedLevels()
    {
        var raw = _cfg.GetCVar(StarlightCCVars.LogSawmillLevels);
        if (string.IsNullOrEmpty(raw))
            return;

        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var sep = entry.IndexOf('=');
            if (sep <= 0)
                continue;

            var name = entry[..sep];
            var levelStr = entry[(sep + 1)..];

            if (!Enum.TryParse<LogLevel>(levelStr, out var level))
                continue;

            var sawmill = _logManager.GetSawmill(name);
            sawmill.Level = level;
        }
    }
}

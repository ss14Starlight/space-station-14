using Content.Shared._Starlight.Physics.Diagnostics;
using Robust.Client.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Physics.Diagnostics;

/// <summary>
/// Saves physics reports from the server into this client's data directory.
/// </summary>
public sealed partial class PhysicsReportSaveSystem : EntitySystem
{
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IResourceManager _resources = default!;

    // Reports are tens of KB; the cap only stops a server from filling the disk.
    private const int MaxReportChars = 1_000_000;

    private static readonly ResPath _reportDir = new("/physics_reports");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PhysicsReportReadyEvent>(OnReportReady);
    }

    private void OnReportReady(PhysicsReportReadyEvent ev)
    {
        var report = ev.Report.Length > MaxReportChars ? ev.Report[..MaxReportChars] : ev.Report;
        var path = _reportDir / $"physics_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        try
        {
            _resources.UserData.CreateDir(_reportDir);
            _resources.UserData.WriteAllText(path, report);
        }
        catch (Exception e)
        {
            Log.Error($"Could not save physics report: {e}");
            _console.WriteError(null, Loc.GetString("cmd-physicsreport-save-failed"));
            return;
        }

        _console.WriteLine(null, Loc.GetString("cmd-physicsreport-saved",
            ("path", path.ToString()),
            ("folder", _resources.UserData.RootDir ?? Loc.GetString("cmd-physicsreport-data-folder"))));
    }
}

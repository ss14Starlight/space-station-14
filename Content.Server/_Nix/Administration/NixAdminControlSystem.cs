using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.AlertLevel;
using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server._Starlight.SecureTerminal;
using Content.Server._Starlight.Shipyard.Systems;
using Content.Shared._Nix.Administration;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Server.Upload;
using Robust.Shared.Asynchronous;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Nix.Administration;

public sealed partial class NixAdminControlSystem : EntitySystem
{
    private const float AdminAudioSafetyReduction = -8f;

    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ITaskManager _tasks = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private ServerGlobalSoundSystem _globalSound = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private NetworkResourceManager _networkResources = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SecureCommandTerminalSystem _secureTerminal = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private ShipyardSystem _shipyard = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private ChatSystem _chat = default!;

    private readonly HashSet<(NetUserId User, string Path)> _pendingUploads = new();
    private readonly Dictionary<NetUserId, string> _preparedUploads = new();
    private bool _shuttleSelectionLocked;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<NixAdminAudioCommandEvent>(OnAudioCommand);
        SubscribeNetworkEvent<NixAdminPowerCommandEvent>(OnPowerCommand);
        SubscribeNetworkEvent<NixAdminAlertRequestEvent>(OnAlertRequest);
        SubscribeNetworkEvent<NixAdminSetAlertEvent>(OnSetAlert);
        SubscribeNetworkEvent<NixAdminSecureActionEvent>(OnSecureAction);
        SubscribeNetworkEvent<NixAdminShuttleRequestEvent>(OnShuttleRequest);
        SubscribeNetworkEvent<NixAdminShuttleCommandEvent>(OnShuttleCommand);
        SubscribeLocalEvent<RoundEndSystemChangedEvent>(_ =>
        {
            if (_roundEnd.IsRoundEndRequested())
                _shuttleSelectionLocked = true;
        });
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _shuttleSelectionLocked = false);
        _networkResources.ResourcesUploaded += OnResourcesUploaded;
    }

    public override void Shutdown()
    {
        _networkResources.ResourcesUploaded -= OnResourcesUploaded;
        base.Shutdown();
    }

    private void OnAudioCommand(NixAdminAudioCommandEvent ev, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
            return;

        var filter = Filter.Empty().AddAllPlayers(_players);
        if (ev.Action == NixAdminAudioAction.StopAll)
        {
            RaiseNetworkEvent(new NixStopAdminAudioEvent(), filter);
            SendAudioResult(args.SenderSession, NixAdminAudioResult.Stopped);
            return;
        }

        var uploaded = ev.Action == NixAdminAudioAction.PrepareUpload ||
                       ev.Path.StartsWith("/Uploaded/Audio/_Nix/AdminUploads/", StringComparison.Ordinal);
        if (!TryGetAudioPath(ev.Path, uploaded, out var path) ||
            ev.Action == NixAdminAudioAction.Play && uploaded && !_preparedUploads.ContainsValue(path))
        {
            SendAudioResult(args.SenderSession, NixAdminAudioResult.InvalidPath, ev.Path, ev.VolumePercent);
            return;
        }

        var volume = Math.Clamp(ev.VolumePercent, (byte) 0, (byte) 100);
        if (ev.Action == NixAdminAudioAction.PrepareUpload)
        {
            if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Danger))
            {
                SendAudioResult(args.SenderSession, NixAdminAudioResult.UploadRejected, path, volume);
                return;
            }

            foreach (var key in _pendingUploads.Where(key => key.User == args.SenderSession.UserId).ToList())
                _pendingUploads.Remove(key);
            _pendingUploads.Add((args.SenderSession.UserId, path));
            SendAudioResult(args.SenderSession, NixAdminAudioResult.UploadReady, path, volume);
            return;
        }

        PlayAdminAudio(path, volume);
        SendAudioResult(args.SenderSession, NixAdminAudioResult.Playing, path, volume);
    }

    private void PlayAdminAudio(string path, byte volume)
    {
        var decibels = volume == 0
            ? -80f
            : 20f * MathF.Log10(volume / 100f);
        var audioParams = AudioParams.Default.WithVolume(decibels + AdminAudioSafetyReduction);
        var filter = Filter.Empty().AddAllPlayers(_players);
        _globalSound.PlayAdminGlobal(filter, path, audioParams);
    }

    private static bool TryGetAudioPath(string rawPath, bool uploaded, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath) || rawPath.Contains("..", StringComparison.Ordinal))
            return false;

        var normalized = rawPath.StartsWith('/') ? rawPath : $"/{rawPath}";
        var requiredRoot = uploaded ? "/Uploaded/Audio/_Nix/AdminUploads/" : "/Audio/";
        if (!normalized.StartsWith(requiredRoot, StringComparison.Ordinal) ||
            !(normalized.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
              normalized.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
              uploaded && normalized.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        path = normalized;
        return true;
    }

    private void OnResourcesUploaded(NetworkResourcesUploadedEvent ev)
    {
        _tasks.RunOnMainThread(() => ProcessUploadedResources(ev));
    }

    private void ProcessUploadedResources(NetworkResourcesUploadedEvent ev)
    {
        foreach (var (relative, data) in ev.Files)
        {
            var fullPath = $"/Uploaded/{relative.ToString().TrimStart('/')}";
            if (!_pendingUploads.Remove((ev.Session.UserId, fullPath)))
                continue;

            if (!IsSupportedAudio(data, fullPath))
            {
                SendAudioResult(ev.Session, NixAdminAudioResult.UploadRejected, fullPath);
                continue;
            }

            if (fullPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                ConvertAndPrepareMp3(ev.Session, relative, fullPath, data);
                continue;
            }

            SchedulePreparedAudio(ev.Session, fullPath, data.Length);
        }
    }

    private static bool IsSupportedAudio(byte[] data, string path)
    {
        if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            return data.Length >= 4 && data.AsSpan(0, 4).SequenceEqual("OggS"u8);

        if (path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return data.Length >= 3 &&
                   (data.AsSpan(0, 3).SequenceEqual("ID3"u8) ||
                    data.Length >= 2 && data[0] == 0xff && (data[1] & 0xe0) == 0xe0);
        }

        return data.Length >= 12 &&
               data.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
               data.AsSpan(8, 4).SequenceEqual("WAVE"u8);
    }

    private async void ConvertAndPrepareMp3(
        ICommonSession session,
        ResPath sourceRelative,
        string sourceFullPath,
        byte[] data)
    {
        var converted = await ConvertMp3ToOgg(data);
        _tasks.RunOnMainThread(() =>
        {
            if (converted == null)
            {
                SendAudioResult(session, NixAdminAudioResult.UploadRejected, sourceFullPath);
                return;
            }

            var source = sourceRelative.ToString();
            var convertedRelative = new ResPath($"{source[..^4]}.ogg");
            var convertedFullPath = $"/Uploaded/{convertedRelative.ToString().TrimStart('/')}";
            _networkResources.DistributeResources([(convertedRelative, converted)]);
            SchedulePreparedAudio(session, convertedFullPath, converted.Length);
        });
    }

    private static async Task<byte[]?> ConvertMp3ToOgg(byte[] data)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/ffmpeg",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add("pipe:0");
            startInfo.ArgumentList.Add("-vn");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("libvorbis");
            startInfo.ArgumentList.Add("-q:a");
            startInfo.ArgumentList.Add("4");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("ogg");
            startInfo.ArgumentList.Add("pipe:1");

            using var process = Process.Start(startInfo);
            if (process == null)
                return null;

            await using var output = new MemoryStream();
            var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.StandardInput.BaseStream.WriteAsync(data);
            process.StandardInput.Close();
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());

            return process.ExitCode == 0 && output.Length > 0 ? output.ToArray() : null;
        }
        catch
        {
            return null;
        }
    }

    private void SchedulePreparedAudio(
        ICommonSession session,
        string playPath,
        int dataLength)
    {
        // Give every client enough time to mount the transferred resource before playback.
        var delay = TimeSpan.FromSeconds(Math.Clamp(2d + dataLength / 1_000_000d, 2d, 6d));
        Timer.Spawn(delay, () =>
        {
            _preparedUploads[session.UserId] = playPath;
            SendAudioResult(session, NixAdminAudioResult.Prepared, playPath);
        });
    }

    private void SendAudioResult(
        ICommonSession session,
        NixAdminAudioResult result,
        string path = "",
        byte volume = 0)
    {
        RaiseNetworkEvent(new NixAdminAudioResultEvent
        {
            Result = result,
            Path = path,
            VolumePercent = volume
        }, session.Channel);
    }

    private void OnPowerCommand(NixAdminPowerCommandEvent ev, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
            return;

        var affected = ev.Action switch
        {
            NixAdminPowerAction.RechargeSmes => RechargeStationSmes(),
            NixAdminPowerAction.RestoreStationApcs => RestoreStationApcs(),
            _ => 0
        };

        RaiseNetworkEvent(new NixAdminPowerResultEvent
        {
            Action = ev.Action,
            AffectedCount = affected
        }, args.SenderSession.Channel);
    }

    private int RechargeStationSmes()
    {
        var affected = 0;
        var query = EntityQueryEnumerator<PowerMonitoringDeviceComponent, BatteryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var monitor, out var battery, out var transform))
        {
            if (monitor.Group != PowerMonitoringConsoleGroup.SMES || !IsStationGrid(transform.GridUid))
                continue;

            _battery.SetCharge((uid, battery), battery.MaxCharge);
            affected++;
        }

        return affected;
    }

    private int RestoreStationApcs()
    {
        var affected = 0;
        var query = EntityQueryEnumerator<ApcComponent, BatteryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var apc, out var battery, out var transform))
        {
            if (!IsStationGrid(transform.GridUid))
                continue;

            _battery.SetCharge((uid, battery), battery.MaxCharge);
            if (!apc.MainBreakerEnabled)
                EntityManager.System<ApcSystem>().ApcToggleBreaker(uid, apc);
            affected++;
        }

        return affected;
    }

    private bool IsStationGrid(EntityUid? grid)
    {
        return grid is { } gridUid && TryComp<StationMemberComponent>(gridUid, out _);
    }

    private void OnAlertRequest(NixAdminAlertRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Fun))
            return;

        SendAlertSnapshot(args.SenderSession);
    }

    private void OnSetAlert(NixAdminSetAlertEvent ev, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Fun) ||
            !TryResolveAdminStation(args.SenderSession, out var station) ||
            !TryComp<AlertLevelComponent>(station, out var alert) ||
            alert.AlertLevels == null ||
            !alert.AlertLevels.Levels.ContainsKey(ev.Level))
        {
            RaiseNetworkEvent(new NixAdminAlertResultEvent { Success = false }, args.SenderSession.Channel);
            return;
        }

        if (alert.CurrentLevel == ev.Level)
        {
            alert.IsLevelLocked = ev.Locked;
        }
        else
        {
            // actor is intentionally omitted so the public announcement remains in-character.
            _alertLevel.SetLevel(station, ev.Level, true, true, true, ev.Locked, component: alert);
        }

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Admin {args.SenderSession.Name} set {Name(station)} alert level to {ev.Level} (locked: {ev.Locked}).");

        SendAlertSnapshot(args.SenderSession);
        RaiseNetworkEvent(new NixAdminAlertResultEvent
        {
            Success = true,
            Station = GetNetEntity(station),
            Level = ev.Level,
            Locked = ev.Locked
        }, args.SenderSession.Channel);
    }

    private void SendAlertSnapshot(ICommonSession session)
    {
        var snapshot = new NixAdminAlertSnapshotEvent();
        if (TryResolveAdminStation(session, out var station) &&
            TryComp<AlertLevelComponent>(station, out var alert) &&
            alert.AlertLevels != null)
        {
            snapshot.Stations.Add(new NixAdminStationAlertData
            {
                Station = GetNetEntity(station),
                Name = Name(station),
                CurrentLevel = alert.CurrentLevel,
                Locked = alert.IsLevelLocked,
                Levels = alert.AlertLevels.Levels
                    .Select(pair => new NixAdminAlertLevelData
                    {
                        Id = pair.Key,
                        Selectable = pair.Value.Selectable && !pair.Value.DisableSelection
                    })
                    .ToList()
            });
        }
        RaiseNetworkEvent(snapshot, session.Channel);
    }

    private void OnSecureAction(NixAdminSecureActionEvent ev, EntitySessionEventArgs args)
    {
        var success = _admin.HasAdminFlag(args.SenderSession, AdminFlags.Danger) &&
                      TryResolveAdminStation(args.SenderSession, out var station) &&
                      _secureTerminal.ExecuteAdminAction(station, ev.RequestId);

        if (success)
            _adminLog.Add(LogType.Action, LogImpact.Extreme,
                $"Admin {args.SenderSession.Name} immediately executed Secure Terminal action {ev.RequestId}.");

        RaiseNetworkEvent(new NixAdminSecureActionResultEvent
        {
            Success = success,
            RequestId = ev.RequestId
        }, args.SenderSession.Channel);
    }

    private bool TryResolveAdminStation(ICommonSession session, out EntityUid station)
    {
        station = default;
        if (session.AttachedEntity is { } attached &&
            _station.GetOwningStation(attached) is { } owning &&
            HasComp<AlertLevelComponent>(owning))
        {
            station = owning;
            return true;
        }

        var query = EntityQueryEnumerator<AlertLevelComponent>();
        if (!query.MoveNext(out var fallback, out _))
            return false;

        station = fallback;
        return true;
    }

    private void OnShuttleRequest(NixAdminShuttleRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Round))
            return;

        SendShuttleSnapshot(args.SenderSession);
    }

    private void OnShuttleCommand(NixAdminShuttleCommandEvent ev, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, AdminFlags.Round))
            return;

        var success = ev.Action switch
        {
            NixAdminShuttleAction.Call => CallShuttle(args.SenderSession, ev.Seconds),
            NixAdminShuttleAction.Recall => RecallShuttle(args.SenderSession),
            NixAdminShuttleAction.SelectEmergencyShuttle =>
                _admin.HasAdminFlag(args.SenderSession, AdminFlags.Danger) &&
                SelectEmergencyShuttle(args.SenderSession, ev.ShuttlePath),
            NixAdminShuttleAction.SetPurchaseLock =>
                _admin.HasAdminFlag(args.SenderSession, AdminFlags.Danger) &&
                SetPurchaseLock(args.SenderSession, ev.Locked),
            _ => false
        };

        RaiseNetworkEvent(new NixAdminShuttleResultEvent
        {
            Success = success,
            Action = ev.Action
        }, args.SenderSession.Channel);
        SendShuttleSnapshot(args.SenderSession);
    }

    private bool CallShuttle(ICommonSession session, float seconds)
    {
        if (_roundEnd.IsRoundEndRequested() || seconds is < 1f or > 86400f)
            return false;

        _roundEnd.RequestRoundEnd(TimeSpan.FromSeconds(seconds), session.AttachedEntity, false);
        return _roundEnd.IsRoundEndRequested();
    }

    private bool RecallShuttle(ICommonSession session)
    {
        if (!_roundEnd.IsRoundEndRequested())
            return false;

        _roundEnd.CancelRoundEndCountdown(session.AttachedEntity, forceRecall: true);
        return !_roundEnd.IsRoundEndRequested();
    }

    private bool SelectEmergencyShuttle(ICommonSession session, string path)
    {
        if (_shuttleSelectionLocked ||
            !AvailableEmergencyShuttles().Contains(path) ||
            !TryResolveAdminStation(session, out var station) ||
            !_emergencyShuttle.ReplaceEmergencyShuttle(station, new ResPath(path)))
        {
            return false;
        }

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Admin {session.Name} selected emergency shuttle {path}.");
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("nix-admin-shuttle-selected-announcement", ("shuttle", ShuttleDisplayName(path))),
            Loc.GetString("comms-console-announcement-title-centcom"));
        return true;
    }

    private bool SetPurchaseLock(ICommonSession session, bool locked)
    {
        _shipyard.SetPlayerPurchasesLocked(locked);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Admin {session.Name} set player shipyard purchase lock to {locked}.");
        return true;
    }

    private void SendShuttleSnapshot(ICommonSession session)
    {
        var currentPath = string.Empty;
        if (TryResolveAdminStation(session, out var station) &&
            TryComp<StationEmergencyShuttleComponent>(station, out var shuttle))
        {
            currentPath = shuttle.EmergencyShuttlePath.ToString();
        }

        RaiseNetworkEvent(new NixAdminShuttleSnapshotEvent
        {
            Called = _roundEnd.IsRoundEndRequested(),
            SelectionLocked = _shuttleSelectionLocked,
            SecondsRemaining = (float) Math.Max(0d, _roundEnd.ShuttleTimeLeft?.TotalSeconds ?? 0d),
            PurchasesLocked = _shipyard.PlayerPurchasesLocked,
            CurrentShuttlePath = currentPath,
            AvailableShuttles = AvailableEmergencyShuttles()
        }, session.Channel);
    }

    private List<string> AvailableEmergencyShuttles()
    {
        return _resources.ContentFindFiles("/Maps/")
            .Select(path => path.ToString())
            .Where(path => path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) &&
                (path.StartsWith("/Maps/Shuttles/emergency", StringComparison.OrdinalIgnoreCase) ||
                 path.StartsWith("/Maps/_Starlight/Shuttles/Evac/", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(ShuttleDisplayName)
            .ToList();
    }

    private static string ShuttleDisplayName(string path)
    {
        var slash = path.LastIndexOf('/');
        var name = slash >= 0 ? path[(slash + 1)..] : path;
        return name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}

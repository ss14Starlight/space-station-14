using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Starlight.Station;
using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class StationInitCommand : ToolshedCommand
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private StationSystem? _station;
    private static readonly string InitText = "Initialized new station with id";
    private static readonly string Advice = "Add more grids to this station by using stations:addgrid.";
    private static readonly string AlreadyStation = "This grid already belongs to a station. Consider using stations:addgrid.";
    private static readonly string NoId = "You must set the ID of this station with stationinit:setid before you can initialize it.";
    private static readonly string NotGrid = "This entity is not a grid.";
    private static readonly string InvalidEntity = "This entity is either deleted or invalid.";

    /// <summary>
    /// Mark the beginning of the chain, attaching BecomesStationMidRoundComponent to the piped grid.
    /// </summary>
    [CommandImplementation("begin")]
    public EntityUid Begin(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (HasComp<StationMemberComponent>(uid))
        {
            ctx.WriteLine(AlreadyStation);
            return uid;
        }
        EnsureComp<BecomesStationMidRoundComponent>(uid);
        return uid;
    }

    [CommandImplementation("setid")]
    public EntityUid SetId(IInvocationContext ctx, [PipedArgument] EntityUid uid, string id)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.Id = id;
        return uid;
    }

    [CommandImplementation("clearbaseprotos")]
    public EntityUid ClearBaseProtos(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.BaseStationProtos.Clear();
        return uid;
    }

    [CommandImplementation("addbaseproto")]
    public EntityUid AddBaseProto(IInvocationContext ctx, [PipedArgument] EntityUid uid, EntProtoId proto)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.BaseStationProtos.Add(proto);
        return uid;
    }

    [CommandImplementation("rmbaseproto")]
    public EntityUid RmBaseProto(IInvocationContext ctx, [PipedArgument] EntityUid uid, EntProtoId proto)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.BaseStationProtos.Remove(proto);
        return uid;
    }

    [CommandImplementation("setallowftl")]
    public EntityUid SetAllowFTL(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool allow)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AllowFTLDestination = allow;
        return uid;
    }

    [CommandImplementation("setuseemergencyshuttle")]
    public EntityUid SetUseEmergencyShuttle(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool use)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.UseEmergencyShuttle = use;
        return uid;
    }

    [CommandImplementation("setusearmories")]
    public EntityUid SetUseArmories(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool use)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.UseArmories = use;
        return uid;
    }

    [CommandImplementation("setusearrivals")]
    public EntityUid SetUseArrivals(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool use)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.UseArrivals = use;
        return uid;
    }

    [CommandImplementation("setallowdungeonspawns")]
    public EntityUid SetAllowDungeonSpawns(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool allow)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AllowDungeonSpawn = allow;
        return uid;
    }

    [CommandImplementation("setallowcargo")]
    public EntityUid SetAllowCargo(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool allow)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AllowCargoShuttle = allow;
        return uid;
    }

    [CommandImplementation("clearallowedgridspawns")]
    public EntityUid ClearAllowedGridSpawns(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AllowedGridSpawns.Clear();
        return uid;
    }

    [CommandImplementation("addallowedgridspawn")]
    public EntityUid AddAllowedGridSpawn(IInvocationContext ctx, [PipedArgument] EntityUid uid, string gridSpawnName)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AllowedGridSpawns.Add(gridSpawnName);
        return uid;
    }

    [CommandImplementation("rmallowedgridspawn")]
    public EntityUid RmAllowedGridSpawn(IInvocationContext ctx, [PipedArgument] EntityUid uid, string gridSpawnName)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AllowedGridSpawns.Remove(gridSpawnName);
        return uid;
    }

    [CommandImplementation("setemergencyshuttlepath")]
    public EntityUid SetEmergencyShuttlePath(IInvocationContext ctx, [PipedArgument] EntityUid uid, string shuttlePath)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.EmergencyShuttleOverridePath = shuttlePath;
        return uid;
    }

    [CommandImplementation("clearjobs")]
    public EntityUid ClearJobs(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AvailableJobs.Clear();
        return uid;
    }

    [CommandImplementation("addjob")]
    public EntityUid AddJob(IInvocationContext ctx, [PipedArgument] EntityUid uid, ProtoId<JobPrototype> job, int count)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AvailableJobs.Add(job, count);
        return uid;
    }

    [CommandImplementation("rmjob")]
    public EntityUid RmJob(IInvocationContext ctx, [PipedArgument] EntityUid uid, ProtoId<JobPrototype> job)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AvailableJobs.Remove(job);
        return uid;
    }

    [CommandImplementation("namegrid")]
    public EntityUid NameGrid(IInvocationContext ctx, [PipedArgument] EntityUid uid, string name)
    {
        if (!EnsureWorkable(ctx, uid, out _)) return uid;
        EntitySystemManager.GetEntitySystem<MetaDataSystem>().SetEntityName(uid, name);
        return uid;
    }

    [CommandImplementation("setallowevents")]
    public EntityUid SetAllowEvents(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool allow)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        comp.AllowEvents = allow;
        return uid;
    }

    [CommandImplementation("initialize")]
    public EntityUid Initialize(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (!EnsureId(ctx, uid, out _)) return uid;
        CreateStation(ctx, uid);
        return uid;
    }

    [CommandImplementation("initializeget")]
    public EntityUid InitializeGet(IInvocationContext ctx, [PipedArgument] EntityUid uid) =>
        !EnsureId(ctx, uid, out _) ? EntityUid.Invalid : CreateStation(ctx, uid);


    [CommandImplementation("begin")]
    public IEnumerable<EntityUid> Begin(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x=>Begin(ctx ,x));

    [CommandImplementation("setid")]
    public IEnumerable<EntityUid> SetId(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string id)
        => uid.Select(x=>SetId(ctx ,x, id));

    [CommandImplementation("clearbaseprotos")]
    public IEnumerable<EntityUid> ClearBaseProtos(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x=>ClearBaseProtos(ctx ,x));

    [CommandImplementation("addbaseproto")]
    public IEnumerable<EntityUid> AddBaseProto(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, EntProtoId proto)
        => uid.Select(x=>AddBaseProto(ctx ,x, proto));

    [CommandImplementation("rmbaseproto")]
    public IEnumerable<EntityUid> RmBaseProto(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, EntProtoId proto)
        => uid.Select(x=>RmBaseProto(ctx ,x, proto));

    [CommandImplementation("setallowftl")]
    public IEnumerable<EntityUid> SetAllowFTL(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, bool allow)
        => uid.Select(x=>SetAllowFTL(ctx ,x, allow));

    [CommandImplementation("setuseemergencyshuttle")]
    public IEnumerable<EntityUid> SetUseEmergencyShuttle(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, bool use)
        => uid.Select(x=>SetUseEmergencyShuttle(ctx ,x, use));

    [CommandImplementation("setusearmories")]
    public IEnumerable<EntityUid> SetUseArmories(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, bool use)
        => uid.Select(x=>SetUseArmories(ctx ,x, use));

    [CommandImplementation("setusearrivals")]
    public IEnumerable<EntityUid> SetUseArrivals(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, bool use)
        => uid.Select(x=>SetUseArrivals(ctx ,x, use));

    [CommandImplementation("setallowdungeonspawns")]
    public IEnumerable<EntityUid> SetAllowDungeonSpawns(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, bool allow)
        => uid.Select(x=>SetAllowDungeonSpawns(ctx ,x, allow));

    [CommandImplementation("setallowcargo")]
    public IEnumerable<EntityUid> SetAllowCargo(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, bool allow)
        => uid.Select(x=>SetAllowCargo(ctx ,x, allow));

    [CommandImplementation("clearallowedgridspawns")]
    public IEnumerable<EntityUid> ClearAllowedGridSpawns(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x=>ClearAllowedGridSpawns(ctx ,x));

    [CommandImplementation("addallowedgridspawn")]
    public IEnumerable<EntityUid> AddAllowedGridSpawn(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string gridSpawnName)
        => uid.Select(x=>AddAllowedGridSpawn(ctx ,x, gridSpawnName));

    [CommandImplementation("rmallowedgridspawn")]
    public IEnumerable<EntityUid> RmAllowedGridSpawn(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string gridSpawnName)
        => uid.Select(x=>RmAllowedGridSpawn(ctx ,x, gridSpawnName));

    [CommandImplementation("setemergencyshuttlepath")]
    public IEnumerable<EntityUid> SetEmergencyShuttlePath(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string shuttlePath)
        => uid.Select(x=>SetEmergencyShuttlePath(ctx ,x, shuttlePath));

    [CommandImplementation("clearjobs")]
    public IEnumerable<EntityUid> ClearJobs(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x=>ClearJobs(ctx ,x));

    [CommandImplementation("addjob")]
    public IEnumerable<EntityUid> AddJob(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, ProtoId<JobPrototype> job, int count)
        => uid.Select(x=>AddJob(ctx ,x, job, count));

    [CommandImplementation("rmjob")]
    public IEnumerable<EntityUid> RmJob(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, ProtoId<JobPrototype> job)
        => uid.Select(x=>RmJob(ctx ,x, job));

    [CommandImplementation("setallowevents")]
    public IEnumerable<EntityUid> SetAllowEvents(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, bool allow)
        => uid.Select(x=>SetAllowEvents(ctx ,x, allow));

    [CommandImplementation("namegrid")]
    public IEnumerable<EntityUid> NameGrid(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string name)
        => uid.Select(x => NameGrid(ctx, x, name));

    [CommandImplementation("initialize")]
    public IEnumerable<EntityUid> Initialize(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x=>Initialize(ctx ,x));

    [CommandImplementation("initializeget")]
    public IEnumerable<EntityUid> InitializeGet(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x=>InitializeGet(ctx ,x));

    private bool EnsureWorkable(IInvocationContext ctx, EntityUid uid, [NotNullWhen(true)] out BecomesStationMidRoundComponent? comp)
    {
        comp = null;
        if (uid == EntityUid.Invalid || Deleted(uid))
        {
            ctx.WriteLine(InvalidEntity);
            return false;
        }

        if (HasComp<StationMemberComponent>(uid))
        {
            ctx.WriteLine(AlreadyStation);
            return false;
        }

        if (!HasComp<MapGridComponent>(uid))
        {
            ctx.WriteLine(NotGrid);
            return false;
        }
        _station ??= EntitySystemManager.GetEntitySystem<StationSystem>();
        return TryComp(uid, out comp);
    }

    private bool EnsureId(IInvocationContext ctx, EntityUid uid, [NotNullWhen(true)] out BecomesStationMidRoundComponent? comp)
    {
        if (!EnsureWorkable(ctx, uid, out comp)) return false;
        if (comp.Id is not null) return true;
        ctx.WriteLine(NoId);
        return false;
    }

    private EntityUid CreateStation(IInvocationContext ctx, EntityUid uid)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        var ent = _station?.InitializeNewStationMidRound(uid, comp.BaseStationProtos, comp);
        ctx.WriteLine($"{InitText} {ent}. {Advice}");
        return ent ?? EntityUid.Invalid;
    }
}

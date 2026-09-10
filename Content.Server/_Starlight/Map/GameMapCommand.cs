using System.Linq;
using System.Numerics;
using Content.Server._Starlight.Commands;
using Content.Server._Starlight.Toolshed;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Map;

/// Not to be confused with <see cref="Content.Server.Administration.Commands.LoadGameMapCommand"/>.
[ToolshedCommand]
[AdminCommand(AdminFlags.Admin)]
public sealed class GameMapCommand : ToolshedCommand
{
    private MapLoaderSystem? _loader;
    private MapSystem? _map;

    [CommandImplementation("get")]
    public EntityUid GetMap([PipedArgument] EntityUid uid) => Transform(uid).MapUid ?? EntityUid.Invalid;

    [CommandImplementation("get")]
    public IEnumerable<EntityUid> GetMap([PipedArgument] IEnumerable<EntityUid> uid) => uid.Select(GetMap);

    [CommandImplementation("getid")]
    public EntityUid GetMapById(IInvocationContext ctx, int id)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        if (_map.TryGetMap(new MapId(id), out var map)) return map.Value;
        CommandMarkup.Error(ctx, $"No map with the id {id} was found.");
        return EntityUid.Invalid;
    }

    [CommandImplementation("init")]
    public EntityUid MapInit(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        if (!TryComp<MapComponent>(uid, out var map))
        {
            CommandMarkup.Error(ctx, $"Entity {uid} either does not exist or is not a map.");
            return EntityUid.Invalid;
        }

        if (_map.IsInitialized(map.MapId))
        {
            ctx.WriteLine($"Map ID {map.MapId} is already initialized.");
            return EntityUid.Invalid;
        }

        _map.InitializeMap((uid, map));
        ctx.WriteLine($"Map ID {map.MapId} initialized.");
        return uid;
    }

    [CommandImplementation("init")]
    public IEnumerable<EntityUid> MapInit(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => MapInit(ctx, x));

    [CommandImplementation("initid")]
    public EntityUid MapInit(IInvocationContext ctx, int id)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        var mapId = new MapId(id);
        if (!_map.TryGetMap(mapId, out var map))
        {
            CommandMarkup.Error(ctx, $"No map with the id {id} was found.");
            return EntityUid.Invalid;
        }

        if (_map.IsInitialized(mapId))
        {
            ctx.WriteLine($"Map ID {mapId} is already initialized.");
            return EntityUid.Invalid;
        }

        _map.InitializeMap(mapId);
        ctx.WriteLine($"Map ID {mapId} initialized.");
        return map.Value;
    }

    [CommandImplementation("pause")]
    public EntityUid Pause(IInvocationContext ctx, [PipedArgument] EntityUid uid) =>
        SetPaused(ctx, uid, true) ? uid : EntityUid.Invalid;

    [CommandImplementation("unpause")]
    public EntityUid Unpause(IInvocationContext ctx, [PipedArgument] EntityUid uid) =>
        SetPaused(ctx, uid, false) ? uid : EntityUid.Invalid;

    [CommandImplementation("pause")]
    public IEnumerable<EntityUid> Pause(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => Pause(ctx, x));

    [CommandImplementation("unpause")]
    public IEnumerable<EntityUid> Unpause(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => Unpause(ctx, x));

    [CommandImplementation("pauseid")]
    public EntityUid PauseById(IInvocationContext ctx, int id)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        if (_map.TryGetMap(new MapId(id), out var map))
            return SetPaused(ctx, map.Value, true) ? map.Value : EntityUid.Invalid;
        CommandMarkup.Error(ctx, $"No map with the id {id} was found.");
        return EntityUid.Invalid;
    }

    [CommandImplementation("unpauseid")]
    public EntityUid UnpauseById(IInvocationContext ctx, int id)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        if (_map.TryGetMap(new MapId(id), out var map))
            return SetPaused(ctx, map.Value, false) ? map.Value : EntityUid.Invalid;
        CommandMarkup.Error(ctx, $"No map with the id {id} was found.");
        return EntityUid.Invalid;
    }

    [CommandImplementation("add")]
    public EntityUid AddMap(IInvocationContext ctx, int id, bool init)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        var mapId = new MapId(id);
        if (_map.MapExists(mapId))
        {
            CommandMarkup.Error(ctx, $"Map ID {id} already exists.");
            return EntityUid.Invalid;
        }

        var map = _map.CreateMap(mapId, init);
        ctx.WriteLine($"Map ID {mapId} added.");
        return map;
    }

    [CommandImplementation("rm")]
    public void RemoveMap(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        if (!TryComp<MapComponent>(uid, out var map))
        {
            CommandMarkup.Error(ctx, $"Entity {uid} either does not exist or is not a map.");
            return;
        }
        _map.QueueDeleteMap(map.MapId);
        ctx.WriteLine($"Map ID {map.MapId} has been deleted.");
    }

    [CommandImplementation("rm")]
    public void RemoveMap(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uids)
    {
        // Really there should be a LINQ function similar to Select that doesn't return anything but oh well.
        foreach(var uid in uids)
            RemoveMap(ctx, uid);
    }

    [CommandImplementation("rmid")]
    public void RemoveMap(IInvocationContext ctx, int id)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        var mapId = new MapId(id);
        if (!_map.TryGetMap(mapId, out _))
        {
            CommandMarkup.Error(ctx, $"No map with the id {id} was found.");
            return;
        }
        _map.QueueDeleteMap(mapId);
        ctx.WriteLine($"Map ID {id} has been deleted.");
    }

    [CommandImplementation("load")]
    public EntityUid LoadMap(IInvocationContext ctx, int id, ResPath path, bool useStoredUids)
    {
        _loader ??= EntitySystemManager.GetEntitySystem<MapLoaderSystem>();
        var opts = new DeserializationOptions { StoreYamlUids = useStoredUids };
        if (_loader.TryLoadMapWithId(new MapId(id), path, out var map, out _, opts, Vector2.Zero,
                Angle.Zero)) return map.Value;
        CommandMarkup.Error(ctx, "Unable to load map.");
        return EntityUid.Invalid;
    }

    [CommandImplementation("loadoffset")]
    public EntityUid LoadMapOffset(IInvocationContext ctx, int id, ResPath path, bool useStoredUids, float xOff,
        float yOff, float rotation)
    {
        _loader ??= EntitySystemManager.GetEntitySystem<MapLoaderSystem>();
        var opts = new DeserializationOptions { StoreYamlUids = useStoredUids };
        if (_loader.TryLoadMapWithId(new MapId(id), path, out var map, out _, opts, new Vector2(xOff, yOff),
                Angle.FromDegrees(rotation))) return map.Value;
        CommandMarkup.Error(ctx, "Unable to load map.");
        return EntityUid.Invalid;
    }

    [CommandImplementation("save")]
    public EntityUid SaveMap(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        if (!TryComp<MapComponent>(uid, out var map))
        {
            CommandMarkup.Error(ctx, $"Entity {uid} either does not exist or is not a map.");
            return EntityUid.Invalid;
        }

        return DoSaveMap(ctx, map.MapId, path) ? uid : EntityUid.Invalid;
    }

    [CommandImplementation("saveid")]
    public EntityUid SaveMap(IInvocationContext ctx, int id, string path)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        var mapId = new MapId(id);
        if (!_map.TryGetMap(mapId, out var uid))
        {
            CommandMarkup.Error(ctx, $"No map with the id {id} was found.");
            return EntityUid.Invalid;
        }

        return DoSaveMap(ctx, mapId, path) ? uid.Value : EntityUid.Invalid;
    }

    private bool SetPaused(IInvocationContext ctx, EntityUid uid, bool paused)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        if (!TryComp<MapComponent>(uid, out var map))
        {
            CommandMarkup.Error(ctx, $"Entity {uid} either does not exist or is not a map.");
            return false;
        }

        _map.SetPaused((uid, map), paused);
        ctx.WriteLine($"Map ID {map.MapId} {(paused ? "paused" : "unpaused")}.");
        return true;
    }

    private bool DoSaveMap(IInvocationContext ctx, MapId id, string path)
    {
        _map ??= EntitySystemManager.GetEntitySystem<MapSystem>();
        _loader ??= EntitySystemManager.GetEntitySystem<MapLoaderSystem>();
        if (id == MapId.Nullspace)
        {
            CommandMarkup.Error(ctx, "Cannot save nullspace.");
            return false;
        }

        if (_map.IsInitialized(id))
        {
            CommandMarkup.Warn(ctx, "WARNING!! THIS MAP IS INITIALIZED!! IT WILL NOT SAVE CORRECTLY.");
            ctx.WriteLine("As much as it sucks, you are going to need to remap the entire thing on an uninitialized map. Sorry! :(");
        }

        if(_loader.TrySaveMap(id, new ResPath(path)))
            ctx.WriteLine($"Map saved to {path}.");
        else
            CommandMarkup.Error(ctx, "There was an error saving the map.");
        return true;
    }
}

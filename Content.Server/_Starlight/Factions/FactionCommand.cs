using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Factions;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class FactionCommand : ToolshedCommand
{
    private NpcFactionSystem? _faction;
    
    [CommandImplementation("add")]
    public EntityUid AddFaction([PipedArgument] EntityUid uid, ProtoId<NpcFactionPrototype> faction)
    {
        _faction ??= EntitySystemManager.GetEntitySystem<NpcFactionSystem>();
        _faction.AddFaction(uid, faction);
        return uid;
    }

    [CommandImplementation("remove")]
    public EntityUid RemoveFaction([PipedArgument] EntityUid uid, ProtoId<NpcFactionPrototype> faction)
    {
        _faction ??= EntitySystemManager.GetEntitySystem<NpcFactionSystem>();
        _faction.RemoveFaction(uid, faction);
        return uid;
    }

    [CommandImplementation("aggro")]
    public EntityUid Aggro([PipedArgument] EntityUid uid, EntityUid target)
    {
        _faction ??= EntitySystemManager.GetEntitySystem<NpcFactionSystem>();
        _faction.AggroEntity(uid, target);
        return uid;
    }

    [CommandImplementation("deaggro")]
    public EntityUid DeAggro([PipedArgument] EntityUid uid, EntityUid target)
    {
        _faction ??= EntitySystemManager.GetEntitySystem<NpcFactionSystem>();
        _faction.DeAggroEntity(uid, target);
        return uid;
    }

    [CommandImplementation("ignore")]
    public EntityUid Ignore([PipedArgument] EntityUid uid, EntityUid target)
    {
        _faction ??= EntitySystemManager.GetEntitySystem<NpcFactionSystem>();
        _faction.IgnoreEntity(uid, target);
        return uid;
    }
    
    [CommandImplementation("unignore")]
    public EntityUid UnIgnore([PipedArgument] EntityUid uid, EntityUid target)
    {
        _faction ??= EntitySystemManager.GetEntitySystem<NpcFactionSystem>();
        _faction.UnIgnoreEntity(uid, target);
        return uid;
    }
    
    [CommandImplementation("clear")]
    public EntityUid Clear([PipedArgument] EntityUid uid)
    {
        _faction ??= EntitySystemManager.GetEntitySystem<NpcFactionSystem>();
        _faction.ClearFactions(uid);
        return uid;
    }

    [CommandImplementation("add")]
    public IEnumerable<EntityUid> AddFaction([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<NpcFactionPrototype> faction)
        => uid.Select(x=>AddFaction(x, faction)); 

    [CommandImplementation("remove")]
    public IEnumerable<EntityUid> RemoveFaction([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<NpcFactionPrototype> faction)
        => uid.Select(x=>RemoveFaction(x, faction)); 

    [CommandImplementation("aggro")]
    public IEnumerable<EntityUid> Aggro([PipedArgument] IEnumerable<EntityUid> uid, EntityUid target)
        => uid.Select(x=>Aggro(x, target)); 

    [CommandImplementation("deaggro")]
    public IEnumerable<EntityUid> DeAggro([PipedArgument] IEnumerable<EntityUid> uid, EntityUid target)
        => uid.Select(x=>DeAggro(x, target)); 

    [CommandImplementation("ignore")]
    public IEnumerable<EntityUid> Ignore([PipedArgument] IEnumerable<EntityUid> uid, EntityUid target)
        => uid.Select(x=>Ignore(x, target)); 

    [CommandImplementation("unignore")]
    public IEnumerable<EntityUid> UnIgnore([PipedArgument] IEnumerable<EntityUid> uid, EntityUid target)
        => uid.Select(x=>UnIgnore(x, target)); 

    [CommandImplementation("clear")]
    public IEnumerable<EntityUid> Clear([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Clear); 
}
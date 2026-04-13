using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Administration.Components;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class KillSignCommand : ToolshedCommand
{
    private static readonly string _baseContentPath = "Objects/Misc/killsign.rsi";
    private static readonly string _sLContentPath = "_Starlight/Objects/Misc/killsign.rsi";

    private static readonly Dictionary<string, (string path, string state)> _sprites = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kill"] = (_baseContentPath, "kill"),
        ["raider"] = (_baseContentPath, "raider"),
        ["peak"] = (_baseContentPath, "peak"),
        ["nerd"] = (_baseContentPath, "nerd"),
        ["it"] = (_baseContentPath, "it"),
        ["furry"] = (_baseContentPath, "furry"),
        ["dog"] = (_baseContentPath, "dog"),
        ["cat"] = (_baseContentPath, "cat"),
        ["bald"] = (_baseContentPath, "bald"),
        ["w"] = (_sLContentPath, "w"),
        ["vip"] = (_sLContentPath, "vip"),
        ["ssd"] = (_sLContentPath, "ssd"),
        ["uwu"] = (_sLContentPath, "uwu"),
        ["owo"] = (_sLContentPath, "owo"),
        ["l"] = (_sLContentPath, "l"),
        ["honk"] = (_sLContentPath, "honk"),
        ["event"] = (_sLContentPath, "event"),
        ["dm"] = (_sLContentPath, "dm"),
        ["clueless"] = (_sLContentPath, "clueless"),
        ["admin"] = (_sLContentPath, "admin"),
    };

    /// <summary>
    /// Applies a kill sign to the specified entity UID with the given sprite data.
    /// </summary>
    /// <param name="uid">Target entity UID</param>
    /// <param name="data">Sprite data tuple containing path and state</param>
    /// <returns>The UID of the entity with the applied kill sign</returns>
    private EntityUid ApplyKillSign(EntityUid uid, (string path, string state) data)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(data.path), data.state);
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    /// <summary>
    /// Command which applies a kill sign to the specified entity UID based on the provided type. If the type is unknown, it will notify the user.
    /// </summary>
    /// <param name="ctx">Invocation context</param>
    /// <param name="uid">Target entity UID</param>
    /// <param name="type">Kill sign type</param>
    /// <returns>The UID of the entity with the applied kill sign</returns>
    [CommandImplementation("set")]
    public EntityUid Set(IInvocationContext ctx, [PipedArgument] EntityUid uid, string type)
    {
        if (!_sprites.TryGetValue(type, out var data))
        {
            ctx.WriteLine($"Unknown kill sign type: {type}");
            return uid;
        }

        return ApplyKillSign(uid, data);
    }

    /// <summary>
    /// Command which removes a kill sign on the specified entity UID by removing the KillSignComponent. If the entity does not have a kill sign, this command will have no effect.
    /// </summary>
    /// <param name="uid">Target entity UID</param>
    /// <returns>The UID of the entity with the removed kill sign</returns>
    [CommandImplementation("rm")]
    public EntityUid RemoveKillSign([PipedArgument] EntityUid uid)
    {
        RemComp<KillSignComponent>(uid);
        return uid;
    }

    /// <summary>
    /// List command variation of <seealso cref="Set(IInvocationContext, EntityUid, string)"/>
    /// </summary>
    /// <param name="ctx">Invocation context</param>
    /// <param name="uid">Target entity UIDs</param>
    /// <param name="type">Kill sign type</param>
    /// <returns>The UIDs of the entities with the applied kill signs</returns>
    [CommandImplementation("set")]
    public IEnumerable<EntityUid> Set(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string type)
    {
        if (!_sprites.TryGetValue(type, out var data))
        {
            ctx.WriteLine($"Unknown kill sign type: {type}");
            return uid;
        }

        return uid.Select(x => ApplyKillSign(x, data));
    }

    /// <summary>
    /// List command variation of <seealso cref="RemoveKillSign(EntityUid)"/>
    /// </summary>
    /// <param name="uid">Target entity UIDs</param>
    /// <returns>The UIDs of the entities with the removed kill signs</returns>
    [CommandImplementation("rm")]
    public IEnumerable<EntityUid> RemoveKillSign([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(RemoveKillSign);
}

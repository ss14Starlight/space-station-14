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

    private static readonly Dictionary<string, (string path, string state)> _sprites = new()
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

    private EntityUid ApplyKillSign(EntityUid uid, (string path, string state) data)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(data.path), data.state);
        EntityManager.Dirty(uid, comp);
        return uid;
    }

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

    [CommandImplementation("rm")]
    public EntityUid RemoveKillSign([PipedArgument] EntityUid uid)
    {
        RemComp<KillSignComponent>(uid);
        return uid;
    }

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

    [CommandImplementation("rm")]
    public IEnumerable<EntityUid> RemoveKillSign([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(RemoveKillSign);
}

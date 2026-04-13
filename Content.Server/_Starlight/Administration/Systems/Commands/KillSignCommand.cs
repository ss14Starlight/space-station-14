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
    private static readonly string BaseContentPath = "Objects/Misc/killsign.rsi";
    private static readonly string SLContentPath = "_Starlight/Objects/Misc/killsign.rsi";

    private static readonly Dictionary<string, (string path, string state)> Sprites = new()
    {
        ["kill"] = (BaseContentPath, "kill"),
        ["raider"] = (BaseContentPath, "raider"),
        ["peak"] = (BaseContentPath, "peak"),
        ["nerd"] = (BaseContentPath, "nerd"),
        ["it"] = (BaseContentPath, "it"),
        ["furry"] = (BaseContentPath, "furry"),
        ["dog"] = (BaseContentPath, "dog"),
        ["cat"] = (BaseContentPath, "cat"),
        ["bald"] = (BaseContentPath, "bald"),
        ["w"] = (SLContentPath, "w"),
        ["vip"] = (SLContentPath, "vip"),
        ["ssd"] = (SLContentPath, "ssd"),
        ["uwu"] = (SLContentPath, "uwu"),
        ["owo"] = (SLContentPath, "owo"),
        ["l"] = (SLContentPath, "l"),
        ["honk"] = (SLContentPath, "honk"),
        ["event"] = (SLContentPath, "event"),
        ["dm"] = (SLContentPath, "dm"),
        ["clueless"] = (SLContentPath, "clueless"),
        ["admin"] = (SLContentPath, "admin"),
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
        if (!Sprites.TryGetValue(type, out var data))
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
        if (!Sprites.TryGetValue(type, out var data))
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

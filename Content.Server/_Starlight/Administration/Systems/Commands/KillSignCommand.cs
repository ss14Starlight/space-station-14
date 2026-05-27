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
    private static readonly string _slContentPath = "_Starlight/Objects/Misc/killsign.rsi";

    private static readonly Dictionary<string, (string path, string state)> _sprites =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // base
            ["kill"] = (_baseContentPath, "kill"),
            ["raider"] = (_baseContentPath, "raider"),
            ["peak"] = (_baseContentPath, "peak"),
            ["nerd"] = (_baseContentPath, "nerd"),
            ["it"] = (_baseContentPath, "it"),
            ["furry"] = (_baseContentPath, "furry"),
            ["dog"] = (_baseContentPath, "dog"),
            ["cat"] = (_baseContentPath, "cat"),
            ["bald"] = (_baseContentPath, "bald"),
            ["stinky"] = (_baseContentPath, "stinky"),
            // sl
            ["w"] = (_slContentPath, "w"),
            ["l"] = (_slContentPath, "l"),
            ["vip"] = (_slContentPath, "vip"),
            ["ssd"] = (_slContentPath, "ssd"),
            ["owo"] = (_slContentPath, "owo"),
            ["uwu"] = (_slContentPath, "uwu"),
            ["honk"] = (_slContentPath, "honk"),
            ["moff"] = (_slContentPath, "moff"),
            ["harmbatong"] = (_slContentPath, "harmbatong"),
            ["gay"] = (_slContentPath, "gay"),
            ["fat"] = (_slContentPath, "fat"),
            ["event"] = (_slContentPath, "event"),
            ["dumb"] = (_slContentPath, "dumb"),
            ["blind"] = (_slContentPath, "blind"),
            ["clueless"] = (_slContentPath, "clueless"),
            ["admin"] = (_slContentPath, "admin"),
            ["dm"] = (_slContentPath, "dm"),
            ["point"] = (_slContentPath, "point"),
        };

    [CommandImplementation("set")]
    public EntityUid Set(IInvocationContext ctx, [PipedArgument] EntityUid uid, string type)
    {
        if (_sprites.TryGetValue(type, out var data)) return ApplyKillSign(uid, data);
        ctx.WriteMarkup($"[color=red]Unknown kill sign type: {type}[/color]");
        return uid;
    }

    [CommandImplementation("list")]
    public void List(IInvocationContext ctx) =>
        ctx.WriteLine($"Existing kill signs: {string.Join(", ", _sprites.Keys)}.");

    [CommandImplementation("rm")]
    public EntityUid RemoveKillSign([PipedArgument] EntityUid uid)
    {
        RemComp<KillSignComponent>(uid);
        return uid;
    }

    [CommandImplementation("set")]
    public IEnumerable<EntityUid> Set(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string type)
        => uid.Select(x => Set(ctx, x, type));

    [CommandImplementation("rm")]
    public IEnumerable<EntityUid> RemoveKillSign([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(RemoveKillSign);

    private EntityUid ApplyKillSign(EntityUid uid, (string path, string state) data)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(data.path), data.state);
        EntityManager.Dirty(uid, comp);
        return uid;
    }
}

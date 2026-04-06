using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.Syntax;
using System.Linq; // Starlight

namespace Content.Server.Mind.Toolshed;

/// <summary>
///     Contains various mind-manipulation commands like getting minds, controlling mobs, etc.
/// </summary>
[ToolshedCommand]
public sealed class MindCommand : ToolshedCommand
{
    private SharedMindSystem? _mind;

    [CommandImplementation("get")]
    public MindComponent? Get([PipedArgument] ICommonSession session)
    {
        _mind ??= GetSys<SharedMindSystem>();
        return _mind.TryGetMind(session, out _, out var mind) ? mind : null;
    }

    [CommandImplementation("get")]
    public MindComponent? Get([PipedArgument] EntityUid ent)
    {
        _mind ??= GetSys<SharedMindSystem>();
        return _mind.TryGetMind(ent, out _, out var mind) ? mind : null;
    }

    [CommandImplementation("control")]
    public EntityUid Control(IInvocationContext ctx, [PipedArgument] EntityUid target, ICommonSession player)
    {
        _mind ??= GetSys<SharedMindSystem>();


        if (!_mind.TryGetMind(player, out var mindId, out var mind))
        {
            ctx.ReportError(new SessionHasNoEntityError(player));
            return target;
        }

        _mind.TransferTo(mindId, target, mind: mind);
        return target;
    }

    //Starlight begin
    [CommandImplementation("takeover")]
    public EntityUid Takeover(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _mind ??= GetSys<SharedMindSystem>();
        _mind.ControlMob(ctx.Session!.UserId, uid);
        return uid;
    }

    [CommandImplementation("wipe")]
    public EntityUid Wipe(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _mind ??= GetSys<SharedMindSystem>();
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            ctx.WriteLine("Entity has no mind to wipe.");
            return uid;
        }

        _mind.WipeMind(mindId);
        return uid;
    }

    [CommandImplementation("wipe")]
    public ICommonSession Wipe(IInvocationContext ctx, [PipedArgument] ICommonSession player)
    {
        _mind ??= GetSys<SharedMindSystem>();
        if (!_mind.TryGetMind(player, out var mindId, out var mind))
        {
            ctx.ReportError(new SessionHasNoEntityError(player));
            return player;
        }

        _mind.WipeMind(mindId);
        return player;
    }

    [CommandImplementation("takeoverwipe")]
    public EntityUid TakeoverWipe(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _mind ??= GetSys<SharedMindSystem>();
        _mind.WipeMind(ctx.Session!);
        _mind.ControlMob(ctx.Session!.UserId, uid);
        return uid;
    }

    [CommandImplementation("controlwipe")]
    public EntityUid ControlWipe(IInvocationContext ctx, [PipedArgument] EntityUid uid, ICommonSession player)
    {
        _mind ??= GetSys<SharedMindSystem>();
        _mind.WipeMind(player);
        _mind.ControlMob(player.UserId, uid);
        return uid;
    }

    [CommandImplementation("wipe")]
    public IEnumerable<EntityUid> Wipe(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x => Wipe(ctx, x));

    [CommandImplementation("wipe")]
    public IEnumerable<ICommonSession> Wipe(IInvocationContext ctx, [PipedArgument] IEnumerable<ICommonSession> player)
        => player.Select(x => Wipe(ctx, x));
    //Starlight end
}

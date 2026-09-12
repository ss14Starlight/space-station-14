using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using System.Linq;
using System.Runtime.InteropServices;
using Content.Server.Silicons.StationAi;
using Content.Shared._Starlight.Commands;

namespace Content.Server.Mind.Toolshed;

/// <summary>
///     Contains various mind-manipulation commands like getting minds, controlling mobs, etc.
/// </summary>
[ToolshedCommand]
public sealed class MindCommand : ToolshedCommand
{
    private SharedMindSystem? _mind;
    private StationAiSystem? _ai;

    // Starlight begin: I can't find any reason to get component instead of entity, so changed it to return entity.
    [CommandImplementation("get")]
    public EntityUid Get([PipedArgument] ICommonSession session)
    {
        _mind ??= GetSys<SharedMindSystem>();
        return _mind.TryGetMind(session, out var mind, out _) ? mind : EntityUid.Invalid;
    }

    [CommandImplementation("get")]
    public EntityUid Get([PipedArgument] EntityUid ent)
    {
        _mind ??= GetSys<SharedMindSystem>();
        return _mind.TryGetMind(ent, out var mind, out _) ? mind : EntityUid.Invalid;
    }
    // Starlight end

    [CommandImplementation("control")]
    public EntityUid Control(IInvocationContext ctx, [PipedArgument] EntityUid target, ICommonSession player, [Optional] [DefaultParameterValue(true)] bool tryAi) // Starlight edit
    {
        _mind ??= GetSys<SharedMindSystem>();
        _ai ??= GetSys<StationAiSystem>(); // Starlight
        if (!_mind.TryGetMind(player, out var mindId, out var mind))
        {
            ctx.ReportError(new SessionHasNoEntityError(player));
            return target;
        }

        if (tryAi && _ai.TryControlAI(mindId, target)) return target; // Starlight
        _mind.TransferTo(mindId, target, mind: mind);
        return target;
    }

    #region Starlight

    [CommandImplementation("takeover")]
    public EntityUid Takeover(IInvocationContext ctx, [PipedArgument] EntityUid uid, [Optional] [DefaultParameterValue(true)] bool tryAi)
    {
        _mind ??= GetSys<SharedMindSystem>();
        _ai ??= GetSys<StationAiSystem>();
        if (CommandHelpers.NoSession(ctx) || (tryAi && _mind.TryGetMind(ctx.Session, out var mindId, out _) && _ai.TryControlAI(mindId, uid))) return uid;

        _mind.ControlMob(ctx.Session!.UserId, uid);
        return uid;
    }

    [CommandImplementation("wipe")]
    public EntityUid Wipe(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _mind ??= GetSys<SharedMindSystem>();
        if (!_mind.TryGetMind(uid, out var mindId, out _))
        {
            CommandMarkup.Error(ctx, "Entity has no mind to wipe.");
            return uid;
        }

        _mind.WipeMind(mindId);
        return uid;
    }

    [CommandImplementation("wipe")]
    public ICommonSession Wipe(IInvocationContext ctx, [PipedArgument] ICommonSession player)
    {
        _mind ??= GetSys<SharedMindSystem>();
        if (!_mind.TryGetMind(player, out var mindId, out _))
        {
            ctx.ReportError(new SessionHasNoEntityError(player));
            return player;
        }

        _mind.WipeMind(mindId);
        return player;
    }

    [CommandImplementation("takeoverwipe")]
    public EntityUid TakeoverWipe(IInvocationContext ctx, [PipedArgument] EntityUid uid, [Optional] [DefaultParameterValue(true)] bool tryAi)
    {
        _mind ??= GetSys<SharedMindSystem>();
        _ai ??= GetSys<StationAiSystem>();
        if (CommandHelpers.NoSession(ctx)) return uid;

        if (!_mind.TryGetMind(uid, out var mindId, out _))
        {
            CommandMarkup.Error(ctx, "Entity has no mind to wipe.");
            return uid;
        }

        if (tryAi && _ai.TryControlAI(mindId, uid)) return uid;
        _mind.ControlMob(ctx.Session!.UserId, uid);
        return uid;
    }

    [CommandImplementation("controlwipe")]
    public EntityUid ControlWipe(IInvocationContext ctx, [PipedArgument] EntityUid uid, ICommonSession player, [Optional] [DefaultParameterValue(true)] bool tryAi)
    {
        _mind ??= GetSys<SharedMindSystem>();
        _ai ??= GetSys<StationAiSystem>();

        if (!_mind.TryGetMind(uid, out var mindId, out _))
        {
            CommandMarkup.Error(ctx, "Entity has no mind to wipe.");
            return uid;
        }

        _mind.WipeMind(player);

        if (tryAi && _ai.TryControlAI(mindId, uid)) return uid;
        _mind.ControlMob(player.UserId, uid);
        return uid;
    }

    [CommandImplementation("wipe")]
    public IEnumerable<EntityUid> Wipe(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x => Wipe(ctx, x));

    [CommandImplementation("wipe")]
    public IEnumerable<ICommonSession> Wipe(IInvocationContext ctx, [PipedArgument] IEnumerable<ICommonSession> player)
        => player.Select(x => Wipe(ctx, x));

    #endregion
}

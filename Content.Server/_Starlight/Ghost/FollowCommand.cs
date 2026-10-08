using Content.Server.Administration;
using Content.Shared._Starlight.Commands;
using Content.Shared.Administration;
using Content.Shared.Follower;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Ghost;

[ToolshedCommand]
[AdminCommand(AdminFlags.Admin)]
public sealed class FollowCommand : ToolshedCommand
{
    private FollowerSystem? _follower;

    [CommandImplementation]
    public EntityUid Follow(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (CommandHelpers.NoSession(ctx)) return uid;
        if (ctx.Session?.AttachedEntity is null)
        {
            CommandMarkup.Error(ctx, "Not currently controlling any entity.");
            return uid;
        }

        if (ctx.Session.AttachedEntity == uid)
        {
            CommandMarkup.Error(ctx, "Cannot follow yourself.");
            return uid;
        }

        _follower ??= GetSys<FollowerSystem>();
        _follower.StartFollowingEntity(ctx.Session.AttachedEntity.Value, uid);
        return uid;
    }

    [CommandImplementation]
    public ICommonSession Follow(IInvocationContext ctx, [PipedArgument] ICommonSession session)
    {
        if (CommandHelpers.NoSession(ctx)) return session;
        if (ctx.Session?.AttachedEntity is null)
        {
            CommandMarkup.Error(ctx, "Not currently controlling any entity.");
            return session;
        }

        if (session.AttachedEntity is null)
        {
            CommandMarkup.Error(ctx, $"Target session {session.Name} has no attached entity.");
            return session;
        }

        if (ctx.Session.AttachedEntity == session.AttachedEntity)
        {
            CommandMarkup.Error(ctx, "Cannot follow yourself.");
            return session;
        }

        _follower ??= GetSys<FollowerSystem>();
        _follower.StartFollowingEntity(ctx.Session.AttachedEntity.Value, session.AttachedEntity.Value);
        return session;
    }
}

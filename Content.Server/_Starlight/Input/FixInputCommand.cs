using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared._Starlight.Input;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Input;

[ToolshedCommand]
[AnyCommand]
public sealed class FixInputCommand : ToolshedCommand
{
    [Dependency] private readonly IEntityNetworkManager _net = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    [CommandImplementation]
    public void FixInput(IInvocationContext ctx)
    {
        _net.SendSystemNetworkMessage(new FixInputEvent(), ctx.Session!.Channel);
        ctx.WriteLine($"Refreshed {ctx.Session.Name}'s input context.");
    }
    
    [CommandImplementation]
    public EntityUid FixInput(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (!_admin.IsAdmin(ctx.Session!) && uid != ctx.Session!.AttachedEntity)
        {
            ctx.WriteLine("You cannot run this command on other players unless you are adminned.");
            return uid;
        }

        if (!_player.TryGetSessionByEntity(uid, out var session))
        {
            ctx.WriteLine("There is no session associated with this entity.");
            return uid;
        }
        
        _net.SendSystemNetworkMessage(new FixInputEvent(), session.Channel);
        ctx.WriteLine($"Refreshed {session.Name}'s input context.");
        return uid;
    }

    [CommandImplementation]
    public IEnumerable<EntityUid> FixInput(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(x => FixInput(ctx, x));
}
using System.Linq;
using Content.Server.Administration;
using Content.Server.Audio;
using Content.Shared.Administration;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Broadcast)]
public sealed class GlobalSoundCommand : ToolshedCommand
{
    [CommandImplementation("play")]
    public EntityUid Play(IInvocationContext ctx, [PipedArgument] EntityUid uid, string path, int volume, bool saveToReplay)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            PlayFor(ctx, [actor.PlayerSession], path, volume, saveToReplay);
        return uid;
    }

    [CommandImplementation("play")]
    public ICommonSession Play(IInvocationContext ctx, [PipedArgument] ICommonSession session, string path, int volume, bool saveToReplay)
    {
        PlayFor(ctx, [session], path, volume, saveToReplay);
        return session;
    }

    [CommandImplementation("play")]
    public IEnumerable<EntityUid> Play(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid, string path, int volume, bool saveToReplay)
    {
        var uids = uid.ToList();
        var sessions = uids
            .Select(x => TryComp<ActorComponent>(x, out var actor) ? actor.PlayerSession : null)
            .OfType<ICommonSession>()
            .ToList();

        PlayFor(ctx, sessions, path, volume, saveToReplay);
        return uids;
    }

    [CommandImplementation("play")]
    public IEnumerable<ICommonSession> Play(IInvocationContext ctx, [PipedArgument] IEnumerable<ICommonSession> session, string path, int volume, bool saveToReplay)
    {
        var sessions = session.ToList();
        PlayFor(ctx, sessions, path, volume, saveToReplay);
        return sessions;
    }

    private void PlayFor(IInvocationContext ctx, List<ICommonSession> sessions, string path, int volume, bool saveToReplay)
    {
        if (sessions.Count == 0)
            return;

        var limits = EntityManager.System<AdminLimitSystem>();
        if (!limits.TryUseGlobalSound(ctx.Session, out var remaining))
        {
            ctx.WriteLine(Loc.GetString("cmd-admin-limit-cooldown", ("seconds", MathF.Ceiling(remaining))));
            return;
        }

        var audio = AudioParams.Default.WithVolume(limits.ClampGlobalSoundVolume(ctx.Session, volume));
        EntityManager.System<ServerGlobalSoundSystem>().PlayAdminGlobal(Filter.Empty().AddPlayers(sessions),
            new ResolvedPathSpecifier(path), audio, saveToReplay);

        EntityManager.System<AutoDiscordLogSystem>().LogToDiscord(
            Loc.GetString("autolog-global-sound", ("path", path), ("volume", audio.Volume), ("count", sessions.Count)),
            ctx.Session?.Name ?? "unknown");
    }
}

using System.Linq;
using Content.Server.Administration;
using Content.Server.Audio;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class GlobalSoundCommand : ToolshedCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    [CommandImplementation("play")]
    public EntityUid Play([PipedArgument] EntityUid uid, string path, int volume, bool saveToReplay)
    {
        if (!TryComp<ActorComponent>(uid, out var actor)) return uid;
        var audio = AudioParams.Default.WithVolume(volume);
        EntityManager.System<ServerGlobalSoundSystem>().PlayAdminGlobal(Filter.Empty().AddPlayer(actor.PlayerSession),
            path, audio, saveToReplay);
        return uid;
    }
    
    [CommandImplementation("play")]
    public ICommonSession Play([PipedArgument] ICommonSession session, string path, int volume, bool saveToReplay)
    {
        var audio = AudioParams.Default.WithVolume(volume);
        EntityManager.System<ServerGlobalSoundSystem>().PlayAdminGlobal(Filter.Empty().AddPlayer(session),
            path, audio, saveToReplay);
        return session;
    }

    [CommandImplementation("play")]
    public IEnumerable<EntityUid> Play([PipedArgument] IEnumerable<EntityUid> uid, string path, int volume, bool saveToReplay)
        => uid.Select(x=>Play(x,path,volume,saveToReplay));

    [CommandImplementation("play")]
    public IEnumerable<ICommonSession> Play([PipedArgument] IEnumerable<ICommonSession> session, string path, int volume, bool saveToReplay)
        => session.Select(x => Play(x, path, volume, saveToReplay));
}
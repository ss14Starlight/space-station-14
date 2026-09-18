using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Preferences.Managers;
using Content.Shared._Starlight.Commands;
using Content.Shared._Starlight.Player;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Preferences;

/// <summary>
/// Tells client to open the character setup menu where preferences and character profiles can be edited.
/// The difference is it loads the preference data for another player instead of the ones cached on client.
/// </summary>
[ToolshedCommand]
[AdminCommand(AdminFlags.Admin)]
public sealed partial class EditPlayerPreferencesCommand : ToolshedCommand
{
    [Dependency] private INetManager _net = null!;
    [Dependency] private IPlayerManager _player = null!;
    [Dependency] private IPlayerLocator _locator = null!;

    /*
     * Use the interface here because for reasons beyond my comprehension this allows Toolshed to resolve it.
     * Probably more performant than getting the manager with null propagation maybe possibly IDK lmfao.
     */
    [Dependency] private IServerPreferencesManager _preferences = null!;

    private async Task SendPrefsMessageAsync(IInvocationContext ctx, MinimalPlayerInfo target)
    {
        if (_preferences is not ServerPreferencesManager preferences) return;
        var cts = new CancellationTokenSource();
        var data = await preferences.GetProfileDataForPlayerAsync(target.UserId, cts.Token);
        cts.Token.ThrowIfCancellationRequested();
        if (data is null)
        {
            CommandMarkup.Error(ctx, $"Unable to find data for player {target.UserId}.");
            return;
        }

        var msg = new MsgOpenPlayerCharacterSetup { Preferences = data, PlayerInfo = target };
        _net.ServerSendMessage(msg, ctx.Session!.Channel);
    }

    [CommandImplementation]
    public ICommonSession EditPrefs(IInvocationContext ctx, [PipedArgument] ICommonSession target)
    {
        if (CommandHelpers.NoSession(ctx))
            return target;
        _ = SendPrefsMessageAsync(ctx, new MinimalPlayerInfo(target.Name, target.UserId));
        return target;
    }

    [CommandImplementation]
    public EntityUid EditPrefs(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (CommandHelpers.NoSession(ctx))
            return uid;
        if (!_player.TryGetSessionByEntity(uid, out var session))
        {
            CommandMarkup.Error(ctx,
                $"Entity {EntityManager.ToPrettyString(uid)} does not have an attached session.");
            return uid;
        }

        _ = SendPrefsMessageAsync(ctx, new MinimalPlayerInfo(session.Name, session.UserId));
        return uid;
    }

    private async Task FindAndEditPrefsAsync(IInvocationContext ctx, string player)
    {
        var located = await _locator.LookupIdByNameOrIdAsync(player);
        if (located is null)
        {
            CommandMarkup.Error(ctx,
                $"No player with the name/ID {player} was found online, offline, or on the auth server.");
            return;
        }

        await SendPrefsMessageAsync(ctx, new MinimalPlayerInfo(located.Username, located.UserId));
    }

    // Attempt to resolve an offline player. ICommonSession parser only works for connected players so we use string.
    [CommandImplementation]
    public void EditPrefsOffline(IInvocationContext ctx, string player)
    {
        if (CommandHelpers.NoSession(ctx))
            return;
        _ = FindAndEditPrefsAsync(ctx, player);
    }
}

using System.Threading.Tasks;
using Content.Server._NullLink;
using Content.Server._NullLink.Helpers;
using Content.Server.EUI;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    private readonly HashSet<ICommonSession> _discordPromptOpen = [];

    // ask the cluster for the live link state and nudge the player if no discord is tied to the account
    private void CheckDiscordLink(ICommonSession session)
    {
        if (_discordPromptOpen.Contains(session)
            || !_actors.TryGetServerGrain(out var serverGrain))
            return;

        var url = GetDiscordAuthUrl(session.UserId.ToString());
        if (string.IsNullOrEmpty(url))
            return;

        serverGrain.GetPlayerDiscordId(session.UserId)
            .Then(discordId =>
            {
                if (discordId == 0)
                    _taskManager.RunOnMainThread(() => OpenDiscordPrompt(session, url));
            })
            .FireAndForget(err => _sawmill.Error($"Discord link check failed for {session.UserId}: {err}"));
    }

    private void OpenDiscordPrompt(ICommonSession session, string url)
    {
        if (session.Status == SessionStatus.Disconnected || !_discordPromptOpen.Add(session))
            return;

        var eui = new DiscordLinkEui(this, url);
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    internal void OnDiscordPromptClosed(ICommonSession session)
        => _discordPromptOpen.Remove(session);
}

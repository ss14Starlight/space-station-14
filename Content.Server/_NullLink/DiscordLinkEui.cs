using Content.Server._NullLink.PlayerData;
using Content.Server.EUI;
using Content.Shared._NullLink;

namespace Content.Server._NullLink;

// one-shot popup nudging an unlinked player to link discord; owner clears its dedup set on close
public sealed class DiscordLinkEui(NullLinkPlayerManager owner, string url) : BaseEui
{
    public override DiscordLinkEuiState GetNewState() => new() { Url = url };

    public override void Closed()
    {
        base.Closed();
        owner.OnDiscordPromptClosed(Player);
    }
}

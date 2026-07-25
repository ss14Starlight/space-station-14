using Content.Client.Eui;
using Content.Shared._NullLink;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._NullLink;

[UsedImplicitly]
public sealed class DiscordLinkEui : BaseEui
{
    private readonly DiscordLinkWindow _window = new();

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        if (state is DiscordLinkEuiState linkState)
            _window.SetUrl(linkState.Url);
    }
}

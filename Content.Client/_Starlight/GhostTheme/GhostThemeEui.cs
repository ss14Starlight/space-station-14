using Content.Client.Eui;
using Content.Client.Lobby;
using Content.Shared._Starlight.GhostTheme;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Starlight.GhostTheme;

[UsedImplicitly]
public sealed class GhostThemeEui : BaseEui
{
    [Dependency] private readonly IClientPreferencesManager _preferencesManager = default!;

    private readonly GhostThemeWindow _window;

    public GhostThemeEui()
    {
        _window = new GhostThemeWindow(_preferencesManager);
        
        _window.SelectTheme += slot =>
        {
            base.SendMessage(new GhostThemeSelectedMessage(slot));
        };
        
        _window.SelectColor += color =>
        {
            base.SendMessage(new GhostThemeColorSelectedMessage(color));
        };
    }

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
        
        if (state is not GhostThemeEuiState ghostThemeState)
            return;
        _window.UpdateThemes(ghostThemeState.AvailableThemes);
    }
}

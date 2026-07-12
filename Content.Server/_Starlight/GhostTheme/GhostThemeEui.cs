using Content.Server.EUI;
using Content.Shared._Starlight.GhostTheme;
using Content.Shared.Eui;

namespace Content.Server._Starlight.GhostTheme;

public sealed class GhostThemeEui : BaseEui
{
    private readonly GhostThemeSystem _ghostThemeSystem;
    private readonly HashSet<string> _availableThemes;
    public GhostThemeEui(HashSet<string> availableThemes)
    {
        _ghostThemeSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<GhostThemeSystem>();
        _availableThemes = availableThemes;
    }

    public override GhostThemeEuiState GetNewState() => new(){
        AvailableThemes = _availableThemes
    };

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is GhostThemeSelectedMessage selectedTheme)
        {
            _ghostThemeSystem.ChangeTheme(Player, selectedTheme.ID);
        }
        else if (msg is GhostThemeColorSelectedMessage colorSelected)
        {
            _ghostThemeSystem.ChangeColor(Player, colorSelected.Color);
        }
        else
        {
            Close();
        }
    }

    public override void Closed()
    {
        base.Closed();

        _ghostThemeSystem.CloseEui(Player);
    }
}

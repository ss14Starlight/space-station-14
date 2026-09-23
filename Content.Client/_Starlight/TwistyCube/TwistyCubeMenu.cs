using Content.Shared._Starlight.TwistyCube;

namespace Content.Client._Starlight.TwistyCube;

using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

public sealed class TwistyCubeMenu : DefaultWindow
{
    public event Action<TwistyCubeAction>? OnAction;
    private TwistyCubeControl? _control;

    public TwistyCubeMenu()
    {
        MinSize = SetSize = new Vector2(600, 300);
        Title = Loc.GetString("twistycube-menu-title");

        var grid = new GridContainer { Columns = 1 };
        var buttonGrid = new GridContainer { Columns = 6, Rows = 2 };
        var centerContainer = new CenterContainer();
        centerContainer.AddChild(buttonGrid);
        grid.AddChild(centerContainer);

        var fcwButton = new Button();
        fcwButton.Text = Loc.GetString("twistycube-action-front-cw");
        fcwButton.OnPressed +=_ => OnAction?.Invoke(TwistyCubeAction.FrontClockwise);
        buttonGrid.AddChild(fcwButton);
        
        var fccwButton = new Button();
        fccwButton.Text = Loc.GetString("twistycube-action-front-ccw");
        fccwButton.OnPressed +=_ => OnAction?.Invoke(TwistyCubeAction.FrontCounterClockwise);
        buttonGrid.AddChild(fccwButton);
        
        grid.AddChild(_control = new TwistyCubeControl());

        ContentsContainer.AddChild(grid);
    }

    public void UpdateState(TwistyCubeState state)
    {
        _control?.CubeState = state;
    }
}

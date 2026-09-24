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
        MinSize = SetSize = new Vector2(512, 400);
        Title = Loc.GetString("twistycube-menu-title");

        var grid = new GridContainer { Rows = 2 };
        var buttonGrid = new GridContainer { Columns = 6 };

        var fcwButton = new Button { HorizontalExpand = true };
        fcwButton.Text = Loc.GetString("twistycube-action-front-cw");
        fcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.FrontClockwise);
        buttonGrid.AddChild(fcwButton);

        var fccwButton = new Button { HorizontalExpand = true };
        fccwButton.Text = Loc.GetString("twistycube-action-front-ccw");
        fccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.FrontCounterClockwise);
        buttonGrid.AddChild(fccwButton);

        var lcwButton = new Button { HorizontalExpand = true };
        lcwButton.Text = Loc.GetString("twistycube-action-left-cw");
        lcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.LeftClockwise);
        buttonGrid.AddChild(lcwButton);

        var lccwButton = new Button { HorizontalExpand = true };
        lccwButton.Text = Loc.GetString("twistycube-action-left-ccw");
        lccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.LeftCounterClockwise);
        buttonGrid.AddChild(lccwButton);

        var tcwButton = new Button { HorizontalExpand = true };
        tcwButton.Text = Loc.GetString("twistycube-action-top-cw");
        tcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.TopClockwise);
        buttonGrid.AddChild(tcwButton);

        var tccwButton = new Button { HorizontalExpand = true };
        tccwButton.Text = Loc.GetString("twistycube-action-top-ccw");
        tccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.TopCounterClockwise);
        buttonGrid.AddChild(tccwButton);

        var kcwButton = new Button { HorizontalExpand = true };
        kcwButton.Text = Loc.GetString("twistycube-action-back-cw");
        kcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.BackClockwise);
        buttonGrid.AddChild(kcwButton);

        var kccwButton = new Button { HorizontalExpand = true };
        kccwButton.Text = Loc.GetString("twistycube-action-back-ccw");
        kccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.BackCounterClockwise);
        buttonGrid.AddChild(kccwButton);

        var rcwButton = new Button { HorizontalExpand = true };
        rcwButton.Text = Loc.GetString("twistycube-action-right-cw");
        rcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.RightClockwise);
        buttonGrid.AddChild(rcwButton);

        var rccwButton = new Button { HorizontalExpand = true };
        rccwButton.Text = Loc.GetString("twistycube-action-right-ccw");
        rccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.RightCounterClockwise);
        buttonGrid.AddChild(rccwButton);

        var bcwButton = new Button { HorizontalExpand = true };
        bcwButton.Text = Loc.GetString("twistycube-action-bottom-cw");
        bcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.BottomClockwise);
        buttonGrid.AddChild(bcwButton);

        var bccwButton = new Button { HorizontalExpand = true };
        bccwButton.Text = Loc.GetString("twistycube-action-bottom-ccw");
        bccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.BottomCounterClockwise);
        buttonGrid.AddChild(bccwButton);

        grid.AddChild(buttonGrid);

        grid.AddChild(_control = new TwistyCubeControl());

        ContentsContainer.AddChild(grid);
    }

    public void UpdateState(TwistyCubeState state)
    {
        _control?.CubeState = state;
    }
}

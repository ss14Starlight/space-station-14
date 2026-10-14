using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Content.Shared._Starlight.TwistyCube;

namespace Content.Client._Starlight.TwistyCube;

public sealed class TwistyCubeMenu : DefaultWindow
{
    public event Action<TwistyCubeAction>? OnAction;
    private readonly TwistyCubeControl? _control;

    public TwistyCubeMenu()
    {
        MinSize = SetSize = new Vector2(512, 460);
        Title = Loc.GetString("twistycube-menu-title");

        var grid = new GridContainer { Rows = 4 };
        var buttonGrid = new GridContainer { Columns = 6 };

        var fcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-front-cw") };
        fcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.FrontClockwise);
        buttonGrid.AddChild(fcwButton);

        var fccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-front-ccw") };
        fccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.FrontCounterClockwise);
        buttonGrid.AddChild(fccwButton);

        var lcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-left-cw") };
        lcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.LeftClockwise);
        buttonGrid.AddChild(lcwButton);

        var lccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-left-ccw") };
        lccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.LeftCounterClockwise);
        buttonGrid.AddChild(lccwButton);

        var tcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-top-cw") };
        tcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.TopClockwise);
        buttonGrid.AddChild(tcwButton);

        var tccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-top-ccw") };
        tccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.TopCounterClockwise);
        buttonGrid.AddChild(tccwButton);

        var kcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-back-cw") };
        kcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.BackClockwise);
        buttonGrid.AddChild(kcwButton);

        var kccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-back-ccw") };
        kccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.BackCounterClockwise);
        buttonGrid.AddChild(kccwButton);

        var rcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-right-cw") };
        rcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.RightClockwise);
        buttonGrid.AddChild(rcwButton);

        var rccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-right-ccw") };
        rccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.RightCounterClockwise);
        buttonGrid.AddChild(rccwButton);

        var bcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-bottom-cw") };
        bcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.BottomClockwise);
        buttonGrid.AddChild(bcwButton);

        var bccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-bottom-ccw") };
        bccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.BottomCounterClockwise);
        buttonGrid.AddChild(bccwButton);

        var scwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-s-cw") };
        scwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.SClockwise);
        buttonGrid.AddChild(scwButton);

        var sccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-s-ccw") };
        sccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.SCounterClockwise);
        buttonGrid.AddChild(sccwButton);

        var mcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-m-cw") };
        mcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.MClockwise);
        buttonGrid.AddChild(mcwButton);

        var mccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-m-ccw") };
        mccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.MCounterClockwise);
        buttonGrid.AddChild(mccwButton);

        var ecwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-e-cw") };
        ecwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.EClockwise);
        buttonGrid.AddChild(ecwButton);

        var eccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-e-ccw") };
        eccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.ECounterClockwise);
        buttonGrid.AddChild(eccwButton);

        var xcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-x-cw") };
        xcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.XClockwise);
        buttonGrid.AddChild(xcwButton);

        var xccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-x-ccw") };
        xccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.XCounterClockwise);
        buttonGrid.AddChild(xccwButton);

        var ycwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-y-cw") };
        ycwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.YClockwise);
        buttonGrid.AddChild(ycwButton);

        var yccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-y-ccw") };
        yccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.YCounterClockwise);
        buttonGrid.AddChild(yccwButton);

        var zcwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-z-cw") };
        zcwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.ZClockwise);
        buttonGrid.AddChild(zcwButton);

        var zccwButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-z-ccw") };
        zccwButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.ZCounterClockwise);
        buttonGrid.AddChild(zccwButton);

        grid.AddChild(buttonGrid);

        grid.AddChild(_control = new TwistyCubeControl());

        var scrambleButton = new Button { HorizontalExpand = true, Text = Loc.GetString("twistycube-action-scramble") };
        scrambleButton.OnPressed += _ => OnAction?.Invoke(TwistyCubeAction.Scramble);
        grid.AddChild(scrambleButton);

        ContentsContainer.AddChild(grid);
    }

    /// <summary>
    /// Updates the state of the menu to the given state.
    /// </summary>
    /// <param name="state">The state to update the menu with</param>
    public void UpdateState(TwistyCubeState state) => _control?.CubeState = state;
}

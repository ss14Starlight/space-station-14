using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Starlight.SecureTerminal;

public sealed class SecureTerminalAdminApprovalWindow : DefaultWindow
{
    public readonly Button DenyButton;
    public readonly Button ApproveButton;
    public readonly BoxContainer InformationContainer;
    public readonly BoxContainer AuthorizedByContainer;

    public SecureTerminalAdminApprovalWindow()
    {
        Title = Loc.GetString("secure-terminal-admin-approval-title");
        MinSize = new Vector2(640, 360);
        SetSize = new Vector2(680, 400);

        InformationContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical
        };
        AuthorizedByContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical
        };
        ApproveButton = new Button
        {
            Text = Loc.GetString("secure-terminal-admin-approval-approve")
        };
        DenyButton = new Button
        {
            Text = Loc.GetString("secure-terminal-admin-approval-deny")
        };

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            Children =
            {
                new ScrollContainer
                {
                    VerticalExpand = true,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            Children =
                            {
                                InformationContainer,
                                AuthorizedByContainer
                            }
                        }
                    }
                },
                new Control
                {
                    MinSize = new Vector2(0, 16)
                },
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = AlignMode.Center,
                    Children =
                    {
                        ApproveButton,
                        new Control
                        {
                            MinSize = new Vector2(20, 0)
                        },
                        DenyButton
                    }
                }
            }
        });
    }
}

using System.Numerics;
using Content.Client.Message;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Timing;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Starlight.BreathOrgan.UI;

/// <summary>
/// A copy of a gas tank window, made to change the behaviour in case of organ gas tanks
/// </summary>
public sealed class OrganGasTankWindow
    : BaseWindow
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private readonly RichTextLabel _lblPressure;
    private readonly RichTextLabel _lblInternals;
    private readonly Button _btnInternals;
    private readonly Button _btnEmptyOrgan;
    private readonly Label _topLabel;

    public EntityUid Entity;

    public event Action? OnToggleInternals;
    public event Action? OnEmptyOrgan;

    public OrganGasTankWindow()
    {
        IoCManager.InjectDependencies(this);
        Control contentContainer;
        BoxContainer topContainer;
        TextureButton btnClose;
        var rootContainer = new LayoutContainer { Name = "OrganGasTankRoot" };
        AddChild(rootContainer);

        MouseFilter = MouseFilterMode.Stop;

        var panelTex = _cache.GetTexture("/Textures/Interface/Nano/button.svg.96dpi.png");
        var back = new StyleBoxTexture
        {
            Texture = panelTex,
            Modulate = Color.FromHex("#25252A"),
        };

        back.SetPatchMargin(StyleBox.Margin.All, 10);

        var topPanel = new PanelContainer
        {
            PanelOverride = back,
            MouseFilter = MouseFilterMode.Pass
        };

        var bottomWrap = new LayoutContainer
        {
            Name = "BottomWrap"
        };

        rootContainer.AddChild(topPanel);
        rootContainer.AddChild(bottomWrap);

        LayoutContainer.SetAnchorPreset(topPanel, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetMarginBottom(topPanel, -85);

        LayoutContainer.SetAnchorPreset(bottomWrap, LayoutContainer.LayoutPreset.VerticalCenterWide);
        LayoutContainer.SetGrowHorizontal(bottomWrap, LayoutContainer.GrowDirection.Both);


        var topContainerWrap = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                (topContainer = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical
                }),
                new Control {MinSize = new Vector2(0, 110)}
            }
        };

        rootContainer.AddChild(topContainerWrap);

        LayoutContainer.SetAnchorPreset(topContainerWrap, LayoutContainer.LayoutPreset.Wide);

        var font = _cache.GetFont("/Fonts/Boxfont-round/Boxfont Round.ttf", 13);

        _topLabel = new Label
        {
            FontOverride = font,
           StyleClasses = { StyleClass.LabelKeyText },
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Left,
            Margin = new Thickness(0, 0, 20, 0),
        };

        var topRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 2, 12, 2),
            Children =
            {
                _topLabel,
                (btnClose = new TextureButton
                {
                    StyleClasses = {DefaultWindow.StyleClassWindowCloseButton},
                    VerticalAlignment = VAlignment.Center
                })
            }
        };

        var middle = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#202025") },
            Children =
            {
                (contentContainer = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Margin = new Thickness(8, 4),
                })
            }
        };

        topContainer.AddChild(topRow);
        topContainer.AddChild(new PanelContainer
        {
            MinSize = new Vector2(0, 2),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#525252ff") }
        });
        topContainer.AddChild(middle);
        topContainer.AddChild(new PanelContainer
        {
            MinSize = new Vector2(0, 2),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#525252ff") }
        });


        _lblPressure = new RichTextLabel();
        contentContainer.AddChild(_lblPressure);

        //internals
        _lblInternals = new RichTextLabel
            { MinSize = new Vector2(200, 0), VerticalAlignment = VAlignment.Center };
        _btnInternals = new Button { Text = Loc.GetString("gas-tank-window-internals-toggle-button") };

        contentContainer.AddChild(
            new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(0, 7, 0, 0),
                Children = { _lblInternals, _btnInternals }
            });

        // Separator
        contentContainer.AddChild(new Control
        {
            MinSize = new Vector2(0, 10)
        });

        _btnEmptyOrgan = new Button 
        { 
            Text = Loc.GetString("organ-gas-tank-window-empty-organ-button"),
            Margin = new Thickness(25, 0, 25, 7)
        };
        contentContainer.AddChild(_btnEmptyOrgan);

        // Handlers
        _btnInternals.OnPressed += args =>
        {
            OnToggleInternals?.Invoke();
        };

        _btnEmptyOrgan.OnPressed += args =>
        {
            OnEmptyOrgan?.Invoke();
        };

        btnClose.OnPressed += _ => Close();
    }

    public void SetTitle(string name)
    {
        _topLabel.Text = name;
    }

    public void UpdateState(GasTankBoundUserInterfaceState state)
    {
        _lblPressure.SetMarkup(Loc.GetString("gas-tank-window-tank-pressure-text", ("tankPressure", $"{state.TankPressure:0.##}")));
    }

    public void Update(bool canConnectInternals, bool internalsConnected, float _)
    {
        _btnInternals.Disabled = !canConnectInternals;
        _lblInternals.SetMarkup(Loc.GetString("gas-tank-window-internal-text",
            ("status", Loc.GetString(internalsConnected ? "gas-tank-window-internal-connected" : "gas-tank-window-internal-disconnected"))));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Easier than managing state on any ent changes. Previously this was just ticked on server's GasTankSystem.
        if (_entManager.TryGetComponent(Entity, out GasTankComponent? tank))
        {
            var canConnectInternals = _entManager.System<SharedGasTankSystem>().CanConnectToInternals((Entity, tank));
            _btnInternals.Disabled = !canConnectInternals;
        }

        if (!_btnInternals.Disabled)
        {
            _btnInternals.Disabled = _entManager.System<UseDelaySystem>().IsDelayed(Entity, id: SharedGasTankSystem.GasTankDelay);
        }
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }

    protected override bool HasPoint(Vector2 point)
    {
        return false;
    }
}


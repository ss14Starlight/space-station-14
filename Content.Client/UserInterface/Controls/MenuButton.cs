using System.Numerics;
using Content.Client._Starlight.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.UserInterface.Controls;

public sealed partial class MenuButton : ContainerButton
{
    [Dependency] private IInputManager _inputManager = default!;
    public const string StyleClassLabelTopButton = "topButtonLabel";
    // public const string StyleClassRedTopButton = "topButtonLabel";

    // TODO: KIIIIIILLLLLLLLLLLLLLLLLLLLLLLLLLL --kaylie.
    private static readonly Color ColorNormal = Color.FromHex("#99a7b3"); // primary color[0] + 0.24 L
    private static readonly Color ColorHovered = Color.FromHex("#acbac6"); // primary color[0] + 0.30 L
    private static readonly Color ColorPressed = Color.FromHex("#75838e"); // primary color[0] + 0.12 L

    private const float VertPad = 4f;

    private BoundKeyFunction? _function;
    private readonly BoxContainer _root;
    private readonly TextureRect? _buttonIcon;
    private readonly Label? _buttonLabel;

    public string AppendStyleClass { set => AddStyleClass(value); }
    public Texture? Icon { get => _buttonIcon!.Texture; set => _buttonIcon!.Texture = value; }

    public BoundKeyFunction? BoundKey
    {
        get => _function;
        set
        {
            _function = value;
            _buttonLabel!.Text = _function == null ? "" : BoundKeyHelper.ShortKeyName(_function.Value);
        }
    }

    public BoxContainer ButtonRoot => _root;

    // Starlight-start
    public Vector2 IconScale { set => _buttonIcon!.TextureScale = value; }

    /// <summary>
    /// Whether to show the key binding next to the icon. If false, the key binding will be hidden, but the name will still be shown if ShowName is true.
    /// </summary>
    public bool ShowKey
    {
        get;
        set
        {
            field = value;
            _buttonLabel!.Visible = value || ShowName;
        }
    } = true;

    private Label? _nameLabel;

    public bool ShowName
    {
        get => _nameLabel?.Visible ?? false;
        set
        {
            if (value && _nameLabel == null)
            {
                _nameLabel = new Label { HorizontalExpand = true, Margin = new Thickness(6, 0) };
                _root.AddChild(_nameLabel);
                _buttonLabel!.SetPositionInParent(_root.ChildCount - 1);
            }

            if (_nameLabel != null)
            {
                _nameLabel.Text = ToolTip;
                _nameLabel.Visible = value;
            }

            _root.Orientation = value ? BoxContainer.LayoutOrientation.Horizontal : BoxContainer.LayoutOrientation.Vertical;
            _buttonLabel!.Visible = value || ShowKey;
        }
    }
    // Starlight-end

    public MenuButton()
    {
        IoCManager.InjectDependencies(this);
        _buttonIcon = new TextureRect()
        {
            TextureScale = new Vector2(0.5f, 0.5f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            VerticalExpand = true,
            Margin = new Thickness(0, VertPad),
            ModulateSelfOverride = ColorNormal,
            Stretch = TextureRect.StretchMode.KeepCentered
        };
        _buttonLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HAlignment.Center,
            ModulateSelfOverride = ColorNormal,
            StyleClasses = {StyleClassLabelTopButton}
        };
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                _buttonIcon,
                _buttonLabel
            }
        };
        AddChild(_root);
        ToggleMode = true;
    }

    protected override void EnteredTree()
    {
        _inputManager.OnKeyBindingAdded += OnKeyBindingChanged;
        _inputManager.OnKeyBindingRemoved += OnKeyBindingChanged;
        _inputManager.OnInputModeChanged += OnKeyBindingChanged;
    }

    protected override void ExitedTree()
    {
        _inputManager.OnKeyBindingAdded -= OnKeyBindingChanged;
        _inputManager.OnKeyBindingRemoved -= OnKeyBindingChanged;
        _inputManager.OnInputModeChanged -= OnKeyBindingChanged;
    }


    private void OnKeyBindingChanged(IKeyBinding obj)
    {
        _buttonLabel!.Text = _function == null ? "" : BoundKeyHelper.ShortKeyName(_function.Value);
    }

    private void OnKeyBindingChanged()
    {
        _buttonLabel!.Text = _function == null ? "" : BoundKeyHelper.ShortKeyName(_function.Value);
    }

    protected override void StylePropertiesChanged()
    {
        // colors of children depend on style, so ensure we update when style is changed
        base.StylePropertiesChanged();
        UpdateChildColors();
    }

    private void UpdateChildColors()
    {
        if (_buttonIcon == null || _buttonLabel == null) return;

        // Starlight-start
        if (TryGetStyleProperty<Color>(StarlightStyleProperty.IconColor, out var iconColor))
        {
            _buttonIcon.ModulateSelfOverride = iconColor;
            _buttonLabel.ModulateSelfOverride = iconColor;
            return;
        }
        // Starlight-end

        switch (DrawMode)
        {
            case DrawModeEnum.Normal:
                _buttonIcon.ModulateSelfOverride = ColorNormal;
                _buttonLabel.ModulateSelfOverride = ColorNormal;
                break;

            case DrawModeEnum.Pressed:
                _buttonIcon.ModulateSelfOverride = ColorPressed;
                _buttonLabel.ModulateSelfOverride = ColorPressed;
                break;

            case DrawModeEnum.Hover:
                _buttonIcon.ModulateSelfOverride = ColorHovered;
                _buttonLabel.ModulateSelfOverride = ColorHovered;
                break;

            case DrawModeEnum.Disabled:
                break;
        }
    }


    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateChildColors();
    }
}

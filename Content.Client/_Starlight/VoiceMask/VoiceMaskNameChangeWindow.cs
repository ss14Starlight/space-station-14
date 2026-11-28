using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

//ReSharper disable once CheckNamespace
namespace Content.Client.VoiceMask;

public sealed partial class VoiceMaskNameChangeWindow
{
    private const int RadioIconColumnCount = 10;

    public Action<ProtoId<JobIconPrototype>?>? OnRadioIconChange;

    private readonly Dictionary<ProtoId<JobIconPrototype>, Button> _radioIconButtons = new();

    public void LoadRadioIcons(IPrototypeManager proto, SpriteSystem sprite)
    {
        RadioIconGrid.RemoveAllChildren();
        _radioIconButtons.Clear();

        RadioIconReset.OnPressed += _ => OnRadioIconChange?.Invoke(null);

        var group = new ButtonGroup();
        var icons = proto.EnumeratePrototypes<JobIconPrototype>().Where(icon => icon.AllowSelection).ToList();
        icons.Sort((x, y) => string.Compare(x.LocalizedJobName, y.LocalizedJobName, StringComparison.CurrentCulture));

        var i = 0;
        foreach (var icon in icons)
        {
            var style = StyleClass.ButtonOpenBoth;
            var modulo = i % RadioIconColumnCount;
            if (modulo == 0)
                style = StyleClass.ButtonOpenRight;
            else if (modulo == RadioIconColumnCount - 1)
                style = StyleClass.ButtonOpenLeft;

            var button = new Button
            {
                StyleClasses = { style },
                MaxSize = new Vector2(42, 28),
                Group = group,
                ToolTip = icon.LocalizedJobName,
            };

            button.AddChild(new TextureRect
            {
                Texture = sprite.Frame0(icon.Icon),
                TextureScale = new Vector2(2.5f, 2.5f),
                Stretch = TextureRect.StretchMode.KeepCentered,
            });

            var id = new ProtoId<JobIconPrototype>(icon.ID);
            button.OnPressed += _ => OnRadioIconChange?.Invoke(id);

            RadioIconGrid.AddChild(button);
            _radioIconButtons[id] = button;
            i++;
        }
    }

    public void UpdateRadioIcon(ProtoId<JobIconPrototype>? current)
    {
        foreach (var (id, button) in _radioIconButtons)
        {
            button.Pressed = id == current;
        }
    }
}

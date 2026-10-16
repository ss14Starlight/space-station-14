using Content.Client._Starlight.UserInterface.Controls;
using Content.Client.Options.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Starlight.Options.UI;

/// <summary>
/// A control that allows the user to edit the layout of a bar by selecting the placement of each entry from a dropdown menu. The layout is represented as a string, which can be parsed and serialized using the <see cref="BarLayout"/> class.
/// </summary>
public sealed class BarLayoutEditor : BoxContainer
{
    private static readonly BarPlacement[] Placements =
        [BarPlacement.Pinned, BarPlacement.Auto, BarPlacement.Menu, BarPlacement.Hidden];

    private readonly Dictionary<string, OptionButton> _rows = new();

    public event Action? OnChanged;

    public BarLayoutEditor()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 4;
    }

    public void SetEntries(IEnumerable<BarLayoutEntry> entries)
    {
        RemoveAllChildren();
        _rows.Clear();

        foreach (var entry in entries)
        {
            var button = new OptionButton { MinWidth = 240 };
            foreach (var placement in Placements)
            {
                button.AddItem(Loc.GetString($"ui-options-bar-placement-{placement.ToString().ToLowerInvariant()}"), (int) placement);
            }

            button.OnItemSelected += args =>
            {
                button.SelectId(args.Id);
                OnChanged?.Invoke();
            };

            _rows[entry.Name] = button;
            AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Children =
                {
                    new Label { Text = Loc.GetString(entry.Label), HorizontalExpand = true, VAlign = Label.VAlignMode.Center },
                    button,
                },
            });
        }
    }

    public string Layout
    {
        get
        {
            var layout = new Dictionary<string, BarPlacement>();
            foreach (var (name, button) in _rows)
            {
                layout[name] = (BarPlacement) button.SelectedId;
            }

            return BarLayout.Serialize(layout);
        }
        set
        {
            var layout = BarLayout.Parse(value);
            foreach (var (name, button) in _rows)
            {
                button.SelectId((int) layout.GetValueOrDefault(name, BarPlacement.Auto));
            }
        }
    }
}

public sealed class OptionBarLayoutCVar : BaseOptionCVar<string>
{
    private readonly BarLayoutEditor _editor;

    protected override string Value
    {
        get => _editor.Layout;
        set => _editor.Layout = value;
    }

    public OptionBarLayoutCVar(OptionsTabControlRow controller, IConfigurationManager cfg, CVarDef<string> cVar, BarLayoutEditor editor)
        : base(controller, cfg, cVar)
    {
        _editor = editor;
        editor.OnChanged += ValueChanged;
    }

    protected override bool IsValueEqual(string a, string b)
        => BarLayout.Serialize(BarLayout.Parse(a)) == BarLayout.Serialize(BarLayout.Parse(b));
}

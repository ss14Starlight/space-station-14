using System.Linq;
using System.Text;
using Content.Shared.Starlight.CCVar;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Log;

namespace Content.Client._Starlight.Logs;

public sealed class LogLevelsWindow : DefaultWindow
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private static readonly string[] LevelNames = ["(inherit)", "Verbose", "Debug", "Info", "Warning", "Error", "Fatal"];
    private static readonly LogLevel?[] LevelValues = [null, LogLevel.Verbose, LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Fatal];

    private readonly LineEdit _searchBox;
    private readonly BoxContainer _sawmillList;

    public LogLevelsWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = "Sawmill Log Levels";
        MinSize = new(450, 500);
        SetSize = new(450, 600);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _searchBox = new LineEdit
        {
            PlaceHolder = "Search sawmill...",
            HorizontalExpand = true,
            Margin = new(4, 4, 4, 2),
        };
        _searchBox.OnTextChanged += _ => Rebuild();
        root.AddChild(_searchBox);

        _sawmillList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            Margin = new(4, 2, 4, 4),
        };
        scroll.AddChild(_sawmillList);
        root.AddChild(scroll);

        Contents.AddChild(root);
    }

    protected override void Opened()
    {
        base.Opened();
        Rebuild();
    }

    private void Rebuild()
    {
        _sawmillList.RemoveAllChildren();

        var filter = _searchBox.Text.Trim();
        var sawmills = _logManager.AllSawmills
            .OrderBy(s => s.Name)
            .Where(s => string.IsNullOrEmpty(filter) || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var sawmill in sawmills)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new(2, 1),
            };

            var label = new Label
            {
                Text = sawmill.Name,
                HorizontalExpand = true,
                ClipText = true,
            };

            var combo = new OptionButton { MinWidth = 100 };
            for (var i = 0; i < LevelNames.Length; i++)
                combo.AddItem(LevelNames[i], i);

            var currentIdx = Array.IndexOf(LevelValues, sawmill.Level);
            combo.SelectId(currentIdx >= 0 ? currentIdx : 0);

            var captured = sawmill;
            combo.OnItemSelected += args =>
            {
                combo.SelectId(args.Id);
                captured.Level = LevelValues[args.Id];
                SaveLevels();
            };

            row.AddChild(label);
            row.AddChild(combo);
            _sawmillList.AddChild(row);
        }
    }

    private void SaveLevels()
    {
        var sb = new StringBuilder();
        foreach (var sawmill in _logManager.AllSawmills)
        {
            if (sawmill.Level is { } level)
                sb.Append(sawmill.Name).Append('=').Append(level).Append(';');
        }

        _cfg.SetCVar(StarlightCCVars.LogSawmillLevels, sb.ToString());
        _cfg.SaveToFile();
    }
}

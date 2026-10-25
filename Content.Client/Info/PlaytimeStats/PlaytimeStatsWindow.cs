using System.Linq;
using Content.Client.Players.PlayTimeTracking;
using Content.Client.UserInterface.Controls;
using Content.Shared.Localizations;
using Content.Shared.Roles;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Info.PlaytimeStats;

//starlight start
[GenerateTypedNameReferences]
public sealed partial class PlaytimeStatsWindow : FancyWindow
{
    [Dependency] private JobRequirementsManager _jobRequirementsManager = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    private readonly Color _antagColor = Color.FromHex("#fe7676");
    private readonly Color _ghostColor = Color.FromHex("#c996e0");

    public PlaytimeStatsWindow()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        BuildTabs();
    }

    private void BuildTabs()
    {
        ServerTabs.RemoveAllChildren();

        var currentServer = _jobRequirementsManager.GetCurrentServerName();

        // Tab 1: merged playtime
        var mergedRolePlaytimes = _jobRequirementsManager.FetchPlaytimeByRoles().ToList();
        var mergedAntagPlaytimes = _jobRequirementsManager.FetchPlaytimeByAntags().ToList();
        var mergedTab = BuildPlaytimeTab(
            _jobRequirementsManager.FetchOverallPlaytime(),
            mergedRolePlaytimes,
            _jobRequirementsManager.FetchPlaytimeByDepartments(),
            mergedAntagPlaytimes,
            _jobRequirementsManager.FetchPlaytimeMiscellaneous(mergedRolePlaytimes, mergedAntagPlaytimes));
        ServerTabs.AddChild(mergedTab);
        TabContainer.SetTabTitle(mergedTab, _loc.GetString("ui-playtime-tab-merged"));

        // Tab 2: current server (_originalRoles)
        var originalRoles = _jobRequirementsManager.GetOriginalRoles();
        if (originalRoles.Count > 0)
        {
            var currentTab = BuildServerTab(originalRoles);
            ServerTabs.AddChild(currentTab);
            TabContainer.SetTabTitle(currentTab, _loc.GetString("ui-playtime-tab-current") + " ★");
        }

        // Remaining tabs: NullLink servers grouped by project prefix
        var rolesPerServer = _jobRequirementsManager.GetRolesPerServer();
        var groups = rolesPerServer
            .GroupBy(kv =>
            {
                var dotIdx = kv.Key.IndexOf('.');
                return dotIdx >= 0 ? kv.Key[..dotIdx] : kv.Key;
            })
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var servers = group.ToList();

            if (servers.Count == 1)
            {
                var (serverName, serverRoles) = servers[0];
                var tab = BuildServerTab(serverRoles);
                ServerTabs.AddChild(tab);
                TabContainer.SetTabTitle(tab, serverName);
            }
            else
            {
                var innerTabs = new TabContainer
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };

                foreach (var (serverName, serverRoles) in servers)
                {
                    var tab = BuildServerTab(serverRoles);
                    innerTabs.AddChild(tab);

                    var dotIdx = serverName.IndexOf('.');
                    var subTitle = dotIdx >= 0 ? serverName[(dotIdx + 1)..] : serverName;
                    TabContainer.SetTabTitle(tab, subTitle);
                }

                ServerTabs.AddChild(innerTabs);
                TabContainer.SetTabTitle(innerTabs, group.Key);
            }
        }
    }

    private BoxContainer BuildServerTab(Dictionary<string, TimeSpan> serverRoles)
    {
        var rolePlaytimes = _jobRequirementsManager.FetchPlaytimeByRoles(serverRoles).ToList();
        var departmentPlaytimes = _jobRequirementsManager.FetchPlaytimeByDepartments(serverRoles);
        var antagPlaytimes = _jobRequirementsManager.FetchPlaytimeByAntags(serverRoles).ToList();
        var overallPlaytime = _jobRequirementsManager.FetchOverallPlaytime(serverRoles);
        var miscPlaytimes = _jobRequirementsManager.FetchPlaytimeMiscellaneous(serverRoles, rolePlaytimes, antagPlaytimes);

        return BuildPlaytimeTab(overallPlaytime, rolePlaytimes, departmentPlaytimes, antagPlaytimes, miscPlaytimes);
    }

    private BoxContainer BuildPlaytimeTab(
        TimeSpan overallPlaytime,
        List<KeyValuePair<JobPrototype, TimeSpan>> rolePlaytimes,
        IEnumerable<KeyValuePair<DepartmentPrototype, TimeSpan>> departmentPlaytimes,
        IEnumerable<KeyValuePair<AntagPrototype, TimeSpan>> antagPlaytimes,
        IEnumerable<KeyValuePair<PlayTimeTrackerPrototype, TimeSpan>> miscPlaytimes)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(0, 4, 0, 0),
        };

        // The total, big, like the headers of the character editor tabs.
        container.AddChild(new PanelContainer
        {
            StyleClasses = { "TraitsHeaderPanel" },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Margin = new Thickness(12, 10),
                    Children =
                    {
                        new Label { Text = _loc.GetString("ui-playtime-overall-base"), StyleClasses = { "TraitsTitleLabel" }, HorizontalExpand = true },
                        new Label { Text = ContentLocalizationManager.FormatPlaytime(overallPlaytime), StyleClasses = { "TraitsTitleLabel" } },
                    },
                },
            },
        });

        var search = new LineEdit
        {
            PlaceHolder = _loc.GetString("ui-playtime-search"),
            HorizontalExpand = true,
            Margin = new Thickness(10, 8),
            StyleClasses = { "TraitsSearchInput" },
        };
        container.AddChild(new PanelContainer { StyleClasses = { "TraitsSearchBar" }, Children = { search } });

        var sections = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 10,
            Margin = new Thickness(0, 0, 4, 0),
        };
        container.AddChild(new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { sections },
        });

        var rows = new List<(PlaytimeRow Row, BoxContainer Section)>();
        AddSection(sections, rows, "ui-playtime-section-departments",
            departmentPlaytimes.Select(p => (_loc.GetString(p.Key.Name), p.Value, (Color?) p.Key.Color)));
        AddSection(sections, rows, "ui-playtime-section-roles",
            rolePlaytimes.Select(p => (_loc.GetString(p.Key.Name), p.Value, (Color?) null)));
        AddSection(sections, rows, "ui-playtime-section-antags",
            antagPlaytimes.Select(p => (_loc.GetString(p.Key.Name), p.Value, (Color?) _antagColor)));
        AddSection(sections, rows, "ui-playtime-section-other",
            miscPlaytimes.Select(p => (_loc.GetString(p.Key.Name), p.Value, (Color?) _ghostColor)));

        var empty = new Label
        {
            Text = _loc.GetString("ui-playtime-empty"),
            StyleClasses = { "LabelSubText" },
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 16),
            Visible = rows.Count == 0,
        };
        sections.AddChild(empty);

        search.OnTextChanged += args =>
        {
            var text = args.Text.Trim();
            foreach (var (row, _) in rows)
                row.Visible = text.Length == 0 || row.RoleName.Contains(text, StringComparison.CurrentCultureIgnoreCase);

            // A section with nothing left to show hides with its title.
            foreach (var section in rows.Select(r => r.Section).Distinct())
                section.Visible = rows.Any(r => r.Section == section && r.Row.Visible);

            empty.Visible = !rows.Any(r => r.Row.Visible);
        };

        return container;
    }

    private void AddSection(
        BoxContainer sections,
        List<(PlaytimeRow Row, BoxContainer Section)> rows,
        string title,
        IEnumerable<(string Name, TimeSpan Time, Color? Color)> entries)
    {
        var list = entries
            .Where(e => e.Time > TimeSpan.Zero)
            .OrderByDescending(e => e.Time)
            .ToList();
        if (list.Count == 0)
            return;

        var section = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };
        section.AddChild(new Label
        {
            Text = _loc.GetString(title),
            StyleClasses = { "TraitsCategoryNameLabel" },
            Margin = new Thickness(2, 0, 0, 4),
        });

        // Bars are relative to the longest time in the section.
        var longest = list[0].Time.TotalMinutes;
        foreach (var (name, time, color) in list)
        {
            var row = new PlaytimeRow(name, time, color, longest > 0 ? (float) (time.TotalMinutes / longest) : 0f);
            section.AddChild(row);
            rows.Add((row, section));
        }

        sections.AddChild(section);
    }

    /// <summary>
    /// A role and its time, with a bar showing how it compares to the others of its section.
    /// </summary>
    private sealed class PlaytimeRow : PanelContainer
    {
        private const float BarWidth = 160;

        public readonly string RoleName;

        public PlaytimeRow(string name, TimeSpan time, Color? color, float fraction)
        {
            RoleName = name;
            StyleClasses.Add("TraitsEntryPanel");

            var nameLabel = new Label { Text = name, HorizontalExpand = true, ClipText = true };
            if (color != null)
                nameLabel.FontColorOverride = color;

            AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Margin = new Thickness(10, 5),
                SeparationOverride = 10,
                Children =
                {
                    nameLabel,
                    new PanelContainer
                    {
                        StyleClasses = { "TraitsProgressBarBg" },
                        SetSize = new System.Numerics.Vector2(BarWidth, 8),
                        VerticalAlignment = VAlignment.Center,
                        Children =
                        {
                            new PanelContainer
                            {
                                StyleClasses = { "TraitsProgressBarFill" },
                                HorizontalAlignment = HAlignment.Left,
                                SetWidth = MathF.Max(2, BarWidth * fraction),
                            },
                        },
                    },
                    new Label
                    {
                        Text = ContentLocalizationManager.FormatPlaytime(time),
                        SetWidth = 80,
                        Align = Label.AlignMode.Right,
                    },
                },
            });
        }
    }
}
//starlight end

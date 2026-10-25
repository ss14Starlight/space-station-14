using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI.Roles;
using Content.Shared.Clothing;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public JobPrototype? JobOverride;

    public ProtoId<AntagPrototype>? AntagOverride;

    private readonly List<(string Job, CheckBox Check)> _jobPriorities = new();

    private readonly Dictionary<string, BoxContainer> _jobCategories = new();

    public void RefreshAntags()
    {
        var renderedAntags = Antags.RefreshAntags(Profile);
        UpdateAntagPreferences(renderedAntags);
    }

    private void OnAntagsSelectionChanged(HashSet<ProtoId<AntagPrototype>> antags)
    {
        if (UpdateAntagPreferences(antags))
            ReloadPreview();
    }

    private bool UpdateAntagPreferences(IEnumerable<ProtoId<AntagPrototype>> antags)
    {
        if (Profile is null)
            return false;

        var selectedAntags = antags.ToHashSet();
        if (selectedAntags.SetEquals(Profile.AntagPreferences))
            return false;

        Profile = Profile.WithAntagPreferences(selectedAntags);
        SetDirty();
        return true;
    }

    /// <summary>
    /// Shows the character dressed as the antag, in the gear it previews with.
    /// </summary>
    private void PreviewAntag(ProtoId<AntagPrototype>? antagId)
    {
        AntagOverride = antagId;
        JobOverride = antagId != null
                      && _prototypeManager.TryIndex(antagId, out var antag)
                      && antag.PreviewStartingGear != null
            ? _prototypeManager.EnumeratePrototypes<JobPrototype>().FirstOrDefault(j => j.StartingGear == antag.PreviewStartingGear)
            : null;
        ReloadPreview();
    }

    private void OnAntagLoadoutPressed(ProtoId<AntagPrototype> antagId)
    {
        OpenLoadoutTab(AntagKeyPrefix + antagId.Id);
    }

    /// <summary>
    /// Job rows by job, and the stats label of each department, for search and counts.
    /// </summary>
    private readonly Dictionary<string, Control> _jobEntries = new();
    private readonly Dictionary<string, (Label Stats, List<string> Jobs)> _departmentStats = new();

    public void RefreshJobs()
    {
        JobList.RemoveAllChildren();
        _jobCategories.Clear();
        _jobPriorities.Clear();
        _jobEntries.Clear();
        _departmentStats.Clear();

        var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>()
            .Where(d => !d.EditorHidden)
            .ToList();
        departments.Sort(DepartmentUIComparer.Instance);

        foreach (var department in departments)
        {
            var jobs = department.Roles
                .Select(jobId => _prototypeManager.Index(jobId))
                .Where(job => job.SetPreference)
                .ToArray();
            if (jobs.Length == 0)
                continue;

            Array.Sort(jobs, JobUIComparer.Instance);
            var (category, entries, stats) = BuildDepartment(department);
            _jobCategories[department.ID] = category;
            _departmentStats[department.ID] = (stats, jobs.Select(j => j.ID).ToList());
            JobList.AddChild(category);

            foreach (var job in jobs)
            {
                var entry = BuildJobEntry(job);
                _jobEntries[job.ID] = entry;
                entries.AddChild(entry);
            }
        }

        UpdateJobPreferences();
        FilterJobs();
    }

    /// <summary>
    /// A collapsible department, styled like the antag and trait categories.
    /// </summary>
    private (BoxContainer Category, BoxContainer Entries, Label Stats) BuildDepartment(DepartmentPrototype department)
    {
        var entries = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(8) };
        var content = new PanelContainer { StyleClasses = { "TraitsCategoryContent" }, Children = { entries } };
        var expand = new Label { Text = "▼", StyleClasses = { "TraitsCategoryExpandIcon" }, Margin = new Thickness(0, 0, 8, 0) };
        var stats = new Label { StyleClasses = { "TraitsCategoryStatsLabel" } };
        var header = new Button
        {
            StyleClasses = { "TraitsCategoryHeaderButton" },
            HorizontalExpand = true,
        };
        header.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 6),
            Children =
            {
                expand,
                new Label { Text = Loc.GetString(department.Name), StyleClasses = { "TraitsCategoryNameLabel" }, HorizontalExpand = true },
                stats,
            },
        });
        header.OnPressed += _ =>
        {
            content.Visible = !content.Visible;
            expand.Text = content.Visible ? "▼" : "▶";
        };

        var category = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                new PanelContainer { StyleClasses = { "TraitsCategoryHeader" }, Children = { header } },
                new PanelContainer { PanelOverride = new StyleBoxFlat(department.Color), SetHeight = 3 },
                content,
            },
        };
        return (category, entries, stats);
    }

    private Control BuildJobEntry(JobPrototype job)
    {
        var allowed = _requirements.IsAllowed(job, Profile, out var reason);
        if (!allowed)
        {
            Profile = Profile?.WithoutJob(job);
            SetDirty();
        }

        var check = new CheckBox
        {
            Disabled = !allowed,
            StyleClasses = { "TraitsEntryCheckbox" },
            VerticalAlignment = VAlignment.Center,
        };
        if (!reason.IsEmpty)
        {
            var tooltip = new Tooltip();
            tooltip.SetMessage(reason);
            check.TooltipSupplier = _ => tooltip;
        }

        check.OnToggled += args =>
        {
            Profile = Profile?.WithJob(job.ID, args.Pressed);
            UpdateJobPreferences();
            ReloadPreview();
            SetDirty();
        };
        _jobPriorities.Add((job.ID, check));

        var info = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            Children = { new Label { Text = job.LocalizedName, StyleClasses = { "TraitsEntryNameLabel" } } },
        };

        // A locked job says why instead of what it is.
        var text = new RichTextLabel { StyleClasses = { "TraitsEntryDescriptionLabel" } };
        if (!allowed)
            text.SetMessage(reason);
        else if (job.LocalizedDescription is { } description)
            text.SetMessage(FormattedMessage.FromUnformatted(description));
        info.AddChild(text);

        // Wrap to the room left by the buttons, instead of pushing them off the row.
        text.MaxWidth = 300;
        info.OnResized += () => text.MaxWidth = Math.Max(100, info.Width);

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(10, 6),
            Children =
            {
                check,
                new TextureRect
                {
                    Texture = _sprite.Frame0(_prototypeManager.Index(job.Icon).Icon),
                    TextureScale = new Vector2(2, 2),
                    VerticalAlignment = VAlignment.Center,
                },
                info,
            },
        };

        if (job.Guides is { } guides)
        {
            var guide = new Button
            {
                Text = "?",
                ToolTip = Loc.GetString("humanoid-profile-editor-guidebook-button-tooltip"),
                VerticalAlignment = VAlignment.Center,
            };
            guide.OnPressed += _ => OnOpenGuidebook?.Invoke(guides);
            row.AddChild(guide);
        }

        row.AddChild(BuildLoadoutButton(job));
        return new PanelContainer
        {
            StyleClasses = { "TraitsEntryPanel" },
            Margin = new Thickness(0, 0, 0, 4),
            Children = { row },
        };
    }

    private Button BuildLoadoutButton(JobPrototype job)
    {
        var button = new Button
        {
            Text = Loc.GetString("loadout-window"),
            VerticalAlignment = VAlignment.Center,
        };

        if (!_prototypeManager.TryIndex<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job.ID), out var roleLoadoutProto))
        {
            button.Disabled = true;
            return button;
        }

        button.OnPressed += _ => OpenLoadoutTab(JobKeyPrefix + job.ID);
        return button;
    }

    private void FilterJobs()
    {
        var search = JobSearch.Text.Trim();
        foreach (var (departmentId, (_, jobs)) in _departmentStats)
        {
            var any = false;
            foreach (var jobId in jobs)
            {
                var visible = search.Length == 0
                              || _prototypeManager.Index<JobPrototype>(jobId).LocalizedName.Contains(search, StringComparison.CurrentCultureIgnoreCase);
                _jobEntries[jobId].Visible = visible;
                any |= visible;
            }

            _jobCategories[departmentId].Visible = any;
        }
    }

    private void UpdateJobPreferences()
    {
        foreach (var (jobId, check) in _jobPriorities)
            check.Pressed = Profile?.JobPreferences.Contains(jobId) == true;

        var selected = _jobPriorities.Count(p => p.Check.Pressed);
        JobCountLabel.Text = Loc.GetString("job-tab-selected", ("count", selected));

        foreach (var (stats, jobs) in _departmentStats.Values)
        {
            stats.Text = Loc.GetString("job-department-selected",
                ("selected", jobs.Count(j => Profile?.JobPreferences.Contains(j) == true)),
                ("total", jobs.Count));
        }
    }
}

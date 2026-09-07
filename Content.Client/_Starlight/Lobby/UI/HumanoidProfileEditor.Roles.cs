using System.Linq;
using System.Numerics;
using Content.Client.Lobby.UI.Loadouts;
using Content.Client.Lobby.UI.Roles;
using Content.Shared.Clothing;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public JobPrototype? JobOverride;

    public ProtoId<AntagPrototype>? AntagOverride;

    private LoadoutWindow? _loadoutWindow;

    private readonly List<(string, RequirementsSelector)> _jobPriorities = new();

    private readonly Dictionary<string, BoxContainer> _jobCategories = new();

    public void RefreshLoadouts()
    {
        _loadoutWindow?.Dispose();
    }

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

    private void OnAntagLoadoutPressed(ProtoId<AntagPrototype> antagId)
    {
        if (Profile is null ||
            !_prototypeManager.TryIndex<AntagPrototype>(antagId, out var antag))
        {
            return;
        }

        var antagLoadoutId = antag.RoleLoadout?.FirstOrDefault();
        if (antagLoadoutId == null ||
            !_prototypeManager.TryIndex<RoleLoadoutPrototype>(antagLoadoutId.Value, out var roleLoadoutProto))
        {
            return;
        }

        Profile.Loadouts.TryGetValue(roleLoadoutProto.ID, out var loadout);
        loadout = loadout?.Clone();

        if (loadout == null)
        {
            loadout = new RoleLoadout(roleLoadoutProto.ID);
            loadout.SetDefault(
                Profile,
                _playerManager.LocalSession,
                _prototypeManager,
                force: true);
        }

        OpenAntagLoadout(antag, loadout, roleLoadoutProto);
    }

    public void RefreshJobs()
    {
        JobList.RemoveAllChildren();
        _jobCategories.Clear();
        _jobPriorities.Clear();
        var firstCategory = true;

        var departments = new List<DepartmentPrototype>();
        foreach (var department in _prototypeManager.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (department.EditorHidden)
                continue;

            departments.Add(department);
        }

        departments.Sort(DepartmentUIComparer.Instance);

        var items = new[]
        {
            ("humanoid-profile-editor-antag-preference-yes-button", 0),
            ("humanoid-profile-editor-antag-preference-no-button", 1),
        };

        foreach (var department in departments)
        {
            var departmentName = Loc.GetString(department.Name);

            if (!_jobCategories.TryGetValue(department.ID, out var category))
            {
                category = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Name = department.ID,
                    ToolTip = Loc.GetString(
                        "humanoid-profile-editor-jobs-amount-in-department-tooltip",
                        ("departmentName", departmentName)),
                };

                if (firstCategory)
                {
                    firstCategory = false;
                }
                else
                {
                    category.AddChild(new Control
                    {
                        MinSize = new Vector2(0, 23),
                    });
                }

                category.AddChild(new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#464966") },
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString(
                                "humanoid-profile-editor-department-jobs-label",
                                ("departmentName", departmentName)),
                            Margin = new Thickness(5f, 0, 0, 0),
                        },
                    },
                });

                _jobCategories[department.ID] = category;
                JobList.AddChild(category);
            }

            var jobs = department.Roles
                .Select(jobId => _prototypeManager.Index(jobId))
                .Where(job => job.SetPreference)
                .ToArray();

            Array.Sort(jobs, JobUIComparer.Instance);

            foreach (var job in jobs)
            {
                var jobContainer = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                };

                var selector = new RequirementsSelector
                {
                    Margin = new Thickness(3f, 3f, 3f, 0f),
                };
                selector.OnOpenGuidebook += OnOpenGuidebook;

                var icon = new TextureRect
                {
                    TextureScale = new Vector2(2, 2),
                    VerticalAlignment = VAlignment.Center,
                };
                var jobIcon = _prototypeManager.Index(job.Icon);
                icon.Texture = _sprite.Frame0(jobIcon.Icon);

                var description = job.LocalizedDescription != null
                    ? FormattedMessage.FromUnformatted(job.LocalizedDescription)
                    : FormattedMessage.Empty;
                var allowed = _requirements.IsAllowed(job, Profile, out var reason);

                if (!description.IsEmpty)
                {
                    description.PushNewline();
                    description.PushNewline();
                }

                description.AddMessage(
                    !reason.IsEmpty
                        ? reason
                        : FormattedMessage.FromMarkupPermissive(Loc.GetString("job-no-requirements")));

                selector.Setup(items, job.LocalizedName, 210, description, icon, job.Guides);

                if (!allowed)
                {
                    selector.LockRequirements(description);
                    Profile = Profile?.WithoutJob(job);
                    SetDirty();
                }
                else
                {
                    selector.UnlockRequirements();
                }

                selector.OnSelected += selection =>
                {
                    var include = selection == 0;
                    Profile = Profile?.WithJob(job.ID, include);

                    UpdateJobPreferences();
                    ReloadPreview();
                    SetDirty();
                };

                var loadoutWindowBtn = new Button
                {
                    Text = Loc.GetString("loadout-window"),
                    HorizontalAlignment = HAlignment.Right,
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(3f, 3f, 0f, 0f),
                };

                var collection = IoCManager.Instance!;
                var protoManager = collection.Resolve<IPrototypeManager>();

                if (!protoManager.TryIndex<RoleLoadoutPrototype>(
                        LoadoutSystem.GetJobPrototype(job.ID),
                        out var roleLoadoutProto))
                {
                    loadoutWindowBtn.Disabled = true;
                }
                else
                {
                    loadoutWindowBtn.OnPressed += _ =>
                    {
                        RoleLoadout? loadout = null;
                        if (Profile?.Loadouts.TryGetValue(LoadoutSystem.GetJobPrototype(job.ID), out var savedLoadout) == true)
                            loadout = savedLoadout;

                        loadout = loadout?.Clone();

                        if (loadout == null)
                        {
                            loadout = new RoleLoadout(roleLoadoutProto.ID);
                            loadout.SetDefault(Profile, _playerManager.LocalSession, _prototypeManager);
                        }

                        OpenLoadout(job, loadout, roleLoadoutProto);
                    };
                }

                _jobPriorities.Add((job.ID, selector));
                jobContainer.AddChild(selector);
                jobContainer.AddChild(loadoutWindowBtn);
                category.AddChild(jobContainer);
            }
        }

        UpdateJobPreferences();
    }

    private void UpdateJobPreferences()
    {
        foreach (var (jobId, prioritySelector) in _jobPriorities)
            prioritySelector.Select(Profile?.JobPreferences.Contains(jobId) == true ? 0 : 1);
    }

    private void OpenLoadout(
        JobPrototype? jobProto,
        RoleLoadout roleLoadout,
        RoleLoadoutPrototype roleLoadoutProto)
    {
        _loadoutWindow?.Dispose();
        _loadoutWindow = null;
        var collection = IoCManager.Instance;

        if (collection == null || _playerManager.LocalSession == null || Profile == null)
            return;

        JobOverride = jobProto;
        var session = _playerManager.LocalSession;

        _loadoutWindow = new LoadoutWindow(
            Profile,
            roleLoadout,
            roleLoadoutProto,
            _playerManager.LocalSession,
            collection)
        {
            Title = Loc.GetString("loadout-window-title-loadout", ("job", $"{jobProto?.LocalizedName}")),
        };

        _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
        _loadoutWindow.OpenCenteredLeft();

        _loadoutWindow.OnNameChanged += name =>
        {
            roleLoadout.EntityName = name;
            Profile = Profile.WithLoadout(roleLoadout);
            SetDirty();
        };

        _loadoutWindow.OnLoadoutPressed += (loadoutGroup, loadoutProto) =>
        {
            roleLoadout.AddLoadout(loadoutGroup, loadoutProto, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile.WithLoadout(roleLoadout);
            ReloadPreview();
        };

        _loadoutWindow.OnLoadoutUnpressed += (loadoutGroup, loadoutProto) =>
        {
            roleLoadout.RemoveLoadout(loadoutGroup, loadoutProto, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile.WithLoadout(roleLoadout);
            ReloadPreview();
        };

        JobOverride = jobProto;
        ReloadPreview();

        _loadoutWindow.OnClose += () =>
        {
            JobOverride = null;
            ReloadPreview();
        };

        UpdateJobPreferences();
    }

    private void OpenAntagLoadout(
        AntagPrototype antagProto,
        RoleLoadout roleLoadout,
        RoleLoadoutPrototype roleLoadoutProto)
    {
        _loadoutWindow?.Dispose();
        _loadoutWindow = null;
        var collection = IoCManager.Instance;

        if (collection == null || _playerManager.LocalSession == null || Profile == null)
            return;

        var session = _playerManager.LocalSession;

        _loadoutWindow = new LoadoutWindow(Profile, roleLoadout, roleLoadoutProto, session, collection)
        {
            Title = Loc.GetString(
                "loadout-window-title-loadout",
                ("job", Loc.GetString(antagProto.Name))),
        };

        _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
        _loadoutWindow.OpenCenteredLeft();

        _loadoutWindow.OnNameChanged += name =>
        {
            roleLoadout.EntityName = name;
            Profile = Profile.WithLoadout(roleLoadout);
            SetDirty();
        };

        _loadoutWindow.OnLoadoutPressed += (loadoutGroup, loadoutProto) =>
        {
            roleLoadout.AddLoadout(loadoutGroup, loadoutProto, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile.WithLoadout(roleLoadout);
            ReloadPreview();
        };

        _loadoutWindow.OnLoadoutUnpressed += (loadoutGroup, loadoutProto) =>
        {
            roleLoadout.RemoveLoadout(loadoutGroup, loadoutProto, _prototypeManager);
            _loadoutWindow.RefreshLoadouts(roleLoadout, session, collection);
            Profile = Profile.WithLoadout(roleLoadout);
            ReloadPreview();
        };

        AntagOverride = antagProto.ID;
        JobOverride = antagProto.PreviewStartingGear != null
            ? _prototypeManager.EnumeratePrototypes<JobPrototype>()
                .FirstOrDefault(j => j.StartingGear == antagProto.PreviewStartingGear)
            : null;

        ReloadPreview();

        _loadoutWindow.OnClose += () =>
        {
            AntagOverride = null;
            JobOverride = null;
            ReloadPreview();
        };
    }
}

using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// The loadout tab: every role with a loadout in one list, edited in place with the side preview dressed for it.
/// The loadout buttons of jobs, antags and the species jump here.
/// </summary>
public sealed partial class HumanoidProfileEditor
{
    private const string JobKeyPrefix = "job:";
    private const string AntagKeyPrefix = "antag:";
    private const string SpeciesKey = "species";

    private string? _loadoutKey;
    private RoleLoadout? _editedLoadout;

    /// <summary>
    /// Whether the job and antag overrides of the preview are the loadout tab's, to drop when leaving it.
    /// </summary>
    private bool _loadoutDressing;

    private bool _loadoutTabVisible => TabContainer.CurrentTab == LoadoutTab.GetPositionInParent();

    private void InitializeLoadoutTab()
    {
        LoadoutTab.OnRoleSelected += SelectLoadoutRole;

        LoadoutTab.OnNameChanged += name =>
        {
            if (_editedLoadout == null)
                return;

            _editedLoadout.EntityName = name;
            ApplyEditedLoadout();
            SetDirty();
        };

        LoadoutTab.OnLoadoutPressed += (group, loadout) =>
        {
            if (_editedLoadout == null)
                return;

            _editedLoadout.AddLoadout(group, loadout, _prototypeManager);
            ApplyEditedLoadout();
        };

        LoadoutTab.OnLoadoutUnpressed += (group, loadout) =>
        {
            if (_editedLoadout == null)
                return;

            _editedLoadout.RemoveLoadout(group, loadout, _prototypeManager);
            ApplyEditedLoadout();
        };

        TabContainer.OnTabChanged += _ =>
        {
            if (_loadoutTabVisible)
                DressForLoadout();
            else
                UndressFromLoadout();
        };
    }

    /// <summary>
    /// Switches to the loadout tab with the given role open.
    /// </summary>
    private void OpenLoadoutTab(string key)
    {
        TabContainer.CurrentTab = LoadoutTab.GetPositionInParent();
        SelectLoadoutRole(key);
    }

    /// <summary>
    /// Rebuilds the role list, after the profile, species or prototypes changed.
    /// </summary>
    public void RefreshLoadouts()
    {
        LoadoutTab.ClearRoles();
        if (Profile == null)
        {
            LoadoutTab.ShowNothing();
            return;
        }

        var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>()
            .Where(d => !d.EditorHidden)
            .ToList();
        departments.Sort(DepartmentUIComparer.Instance);

        var keys = new HashSet<string>();
        foreach (var department in departments)
        {
            var jobs = department.Roles
                .Select(id => _prototypeManager.Index(id))
                .Where(job => job.SetPreference && _prototypeManager.HasIndex<RoleLoadoutPrototype>(LoadoutSystem.GetJobPrototype(job.ID)) && keys.Add(JobKeyPrefix + job.ID))
                .ToArray();
            if (jobs.Length == 0)
                continue;

            Array.Sort(jobs, JobUIComparer.Instance);
            LoadoutTab.AddRoleHeader(Loc.GetString(department.Name), department.Color);
            foreach (var job in jobs)
                LoadoutTab.AddRole(JobKeyPrefix + job.ID, job.LocalizedName, _sprite.Frame0(_prototypeManager.Index(job.Icon).Icon));
        }

        var antags = _prototypeManager.EnumeratePrototypes<AntagPrototype>()
            .Where(a => a.SetPreference && a.RoleLoadout?.FirstOrDefault() is { } id && _prototypeManager.HasIndex(id))
            .OrderBy(a => Loc.GetString(a.Name))
            .ToList();
        if (antags.Count > 0)
        {
            LoadoutTab.AddRoleHeader(Loc.GetString("loadout-tab-antags"), null);
            foreach (var antag in antags)
            {
                keys.Add(AntagKeyPrefix + antag.ID);
                LoadoutTab.AddRole(AntagKeyPrefix + antag.ID, Loc.GetString(antag.Name), null);
            }
        }

        if (_prototypeManager.TryIndex(Profile.Species, out var species) && species.Loadout != null)
        {
            keys.Add(SpeciesKey);
            LoadoutTab.AddRoleHeader(Loc.GetString("loadout-tab-species"), null);
            LoadoutTab.AddRole(SpeciesKey, Loc.GetString(species.Name), null);
        }

        // Keeps the open role when it still exists, as loading another profile shouldn't send the tab back to empty.
        if (_loadoutKey != null && keys.Contains(_loadoutKey))
            SelectLoadoutRole(_loadoutKey);
        else
        {
            _loadoutKey = null;
            _editedLoadout = null;
            LoadoutTab.HighlightRole(null);
            LoadoutTab.ShowNothing();
        }
    }

    private void SelectLoadoutRole(string key)
    {
        if (Profile == null || _playerManager.LocalSession is not { } session)
            return;

        if (!TryGetLoadout(key, out var title, out var loadout, out var proto))
            return;

        _loadoutKey = key;
        _editedLoadout = loadout;
        LoadoutTab.HighlightRole(key);
        LoadoutTab.ShowLoadout(title, Profile, loadout, proto, session);
        DressForLoadout();
    }

    /// <summary>
    /// A copy of the profile's loadout for the role, or its default, to edit.
    /// </summary>
    private bool TryGetLoadout(string key, out string title, out RoleLoadout loadout, out RoleLoadoutPrototype proto)
    {
        title = string.Empty;
        loadout = default!;
        proto = default!;
        if (Profile == null)
            return false;

        ProtoId<RoleLoadoutPrototype>? protoId = null;
        if (key.StartsWith(JobKeyPrefix) && _prototypeManager.TryIndex<JobPrototype>(key[JobKeyPrefix.Length..], out var job))
        {
            title = job.LocalizedName;
            protoId = LoadoutSystem.GetJobPrototype(job.ID);
        }
        else if (key.StartsWith(AntagKeyPrefix) && _prototypeManager.TryIndex<AntagPrototype>(key[AntagKeyPrefix.Length..], out var antag))
        {
            title = Loc.GetString(antag.Name);
            protoId = antag.RoleLoadout?.FirstOrDefault();
        }
        else if (key == SpeciesKey && _prototypeManager.TryIndex(Profile.Species, out var species))
        {
            title = Loc.GetString(species.Name);
            protoId = species.Loadout;
        }

        if (protoId == null || !_prototypeManager.TryIndex(protoId.Value, out var found))
            return false;

        proto = found;

        if (key == SpeciesKey)
        {
            loadout = Profile.SpeciesLoadout?.Clone()
                    ?? Profile.GetSpeciesLoadoutOrDefault(_playerManager.LocalSession, _prototypeManager)
                    ?? new RoleLoadout(proto.ID);
        }
        else if (Profile.Loadouts.TryGetValue(proto.ID, out var saved))
        {
            loadout = saved.Clone();
            return true;
        }
        else
        {
            loadout = new RoleLoadout(proto.ID);
        }

        loadout.SetDefault(Profile, _playerManager.LocalSession, _prototypeManager, force: key.StartsWith(AntagKeyPrefix));
        return true;
    }

    private void ApplyEditedLoadout()
    {
        if (Profile == null || _editedLoadout == null || _playerManager.LocalSession is not { } session)
            return;

        Profile = _loadoutKey == SpeciesKey
            ? Profile.WithSpeciesLoadout(_editedLoadout)
            : Profile.WithLoadout(_editedLoadout);
        SetDirty();
        LoadoutTab.Refresh(Profile, _editedLoadout, session);
        ReloadPreview();
    }

    /// <summary>
    /// Dresses the preview for the role being edited.
    /// </summary>
    private void DressForLoadout()
    {
        if (!_loadoutTabVisible || _loadoutKey == null)
            return;

        JobOverride = null;
        AntagOverride = null;
        if (_loadoutKey.StartsWith(JobKeyPrefix))
        {
            JobOverride = _prototypeManager.Index<JobPrototype>(_loadoutKey[JobKeyPrefix.Length..]);
        }
        else if (_loadoutKey.StartsWith(AntagKeyPrefix))
        {
            var antag = _prototypeManager.Index<AntagPrototype>(_loadoutKey[AntagKeyPrefix.Length..]);
            AntagOverride = antag.ID;
            JobOverride = antag.PreviewStartingGear != null
                ? _prototypeManager.EnumeratePrototypes<JobPrototype>().FirstOrDefault(j => j.StartingGear == antag.PreviewStartingGear)
                : null;
        }

        _loadoutDressing = true;
        ReloadPreview();
    }

    private void UndressFromLoadout()
    {
        if (!_loadoutDressing)
            return;

        _loadoutDressing = false;
        JobOverride = null;
        AntagOverride = null;
        ReloadPreview();
    }
}

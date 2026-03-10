using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Weapons.Gunnery;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;

namespace Content.Client._Starlight.Weapons.Gunnery;

public sealed class GunneryConsoleWindow : FancyWindow
{
    // ── Callbacks to BUI ───────────────────────────────────────────────────

    /// <summary>Invoked when the player fires a cannon. Args: (cannon entity, world target).</summary>
    public Action<NetEntity, EntityCoordinates>? OnFireRequested;

    /// <summary>Invoked continuously while player steers a guided projectile.</summary>
    public Action<EntityCoordinates>? OnGuidanceUpdate;

    // ── Controls ───────────────────────────────────────────────────────────

    private readonly GunneryRadarControl _radarControl;
    private readonly ItemList _cannonList;
    private readonly Label _statusLabel;
    private readonly Label _guidanceLabel;

    // ── Cannon list state ──────────────────────────────────────────────────

    private List<CannonBlipData> _cannons = new();

    public GunneryConsoleWindow()
    {
        RobustXamlLoader.Load(this);

        _radarControl  = FindControl<GunneryRadarControl>("RadarControl");
        _cannonList    = FindControl<ItemList>("CannonList");
        _statusLabel   = FindControl<Label>("StatusLabel");
        _guidanceLabel = FindControl<Label>("GuidanceLabel");

        // Wire radar-control callbacks to window-level callbacks.
        _radarControl.OnFireRequested  = (cannon, target) => OnFireRequested?.Invoke(cannon, target);
        _radarControl.OnGuidanceUpdate = target => OnGuidanceUpdate?.Invoke(target);

        // Sync cannon-list selection to radar control.
        _radarControl.OnSelectionChanged = () =>
        {
            SyncListSelectionToRadarSelection();
            UpdateStatus();
        };

        _cannonList.SelectMode = ItemList.ItemListSelectMode.Multiple;
        _cannonList.OnItemSelected   += OnListItemSelected;
        _cannonList.OnItemDeselected += OnListItemDeselected;
    }

    // ── Update state ───────────────────────────────────────────────────────

    public void UpdateState(GunneryConsoleBoundUserInterfaceState state)
    {
        _radarControl.UpdateState(state);
        _cannons = state.Cannons;

        // Rebuild the cannon list with cooldown info.
        _cannonList.Clear();
        foreach (var cannon in _cannons)
        {
            var label = cannon.CooldownSeconds > 0f
                ? $"{cannon.Name} [{cannon.CooldownSeconds:F1}s]"
                : cannon.Name;
            _cannonList.AddItem(label);
        }

        // Restore list selection from radar control.
        SyncListSelectionToRadarSelection();

        // Guidance indicator.
        _guidanceLabel.Text = state.TrackedGuidedProjectile != null
            ? "GUIDANCE ACTIVE"
            : string.Empty;

        UpdateStatus();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void OnListItemSelected(ItemList.ItemListSelectedEventArgs args)
    {
        if (args.ItemIndex < 0 || args.ItemIndex >= _cannons.Count)
            return;

        _radarControl.SelectedCannons.Add(_cannons[args.ItemIndex].Entity);
        UpdateStatus();
    }

    private void OnListItemDeselected(ItemList.ItemListDeselectedEventArgs args)
    {
        if (args.ItemIndex < 0 || args.ItemIndex >= _cannons.Count)
            return;

        _radarControl.SelectedCannons.Remove(_cannons[args.ItemIndex].Entity);
        UpdateStatus();
    }

    private void SyncListSelectionToRadarSelection()
    {
        for (var i = 0; i < _cannons.Count; i++)
            _cannonList[i].Selected = _radarControl.SelectedCannons.Contains(_cannons[i].Entity);
    }

    private void UpdateStatus()
    {
        if (_radarControl.SelectedCannons.Count == 0)
        {
            _statusLabel.Text = "No cannon selected";
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var cannon in _cannons)
        {
            if (!_radarControl.SelectedCannons.Contains(cannon.Entity))
                continue;

            if (sb.Length > 0) sb.Append('\n');
            sb.Append(cannon.CooldownSeconds > 0f
                ? $"{cannon.Name}: COOLDOWN {cannon.CooldownSeconds:F1}s"
                : cannon.Name);
        }
        _statusLabel.Text = sb.Length > 0 ? sb.ToString() : "No cannon selected";
    }
}

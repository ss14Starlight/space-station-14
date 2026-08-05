using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Weapons.Gunnery;
using Content.Shared.Shuttles.Components;
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
    private readonly BoxContainer        _cannonContainer;
    private readonly Label               _guidanceLabel;
    private readonly Label               _noServerLabel;
    private readonly CheckBox            _filterEnergy;
    private readonly CheckBox            _filterRocket;
    private readonly CheckBox            _filterBallistic;
    private readonly CheckBox            _filterUnknown;

    // ── Cannon list state ──────────────────────────────────────────────────

    private List<CannonBlipData> _cannons = [];

    // Mapping from cannon NetEntity → the toggle button in the list.
    private readonly Dictionary<NetEntity, Button> _cannonButtons = new();

    public GunneryConsoleWindow()
    {
        RobustXamlLoader.Load(this);

        _radarControl    = FindControl<GunneryRadarControl>("RadarControl");
        _cannonContainer = FindControl<BoxContainer>("CannonContainer");
        _guidanceLabel   = FindControl<Label>("GuidanceLabel");
        _noServerLabel   = FindControl<Label>("NoServerLabel");
        _filterEnergy    = FindControl<CheckBox>("FilterEnergy");
        _filterRocket    = FindControl<CheckBox>("FilterRocket");
        _filterBallistic = FindControl<CheckBox>("FilterBallistic");
        _filterUnknown   = FindControl<CheckBox>("FilterUnknown");

        // Wire radar-control callbacks to window-level callbacks.
        _radarControl.OnFireRequested  = (cannon, target) => OnFireRequested?.Invoke(cannon, target);
        _radarControl.OnGuidanceUpdate = target => OnGuidanceUpdate?.Invoke(target);

        // Sync cannon-list selection to radar control.
        _radarControl.OnSelectionChanged = () =>
        {
            SyncButtonSelectionToRadarSelection();
        };

        // Category filter changes → rebuild visible list.
        _filterEnergy.OnToggled    += _ => RebuildCannonButtons();
        _filterRocket.OnToggled    += _ => RebuildCannonButtons();
        _filterBallistic.OnToggled += _ => RebuildCannonButtons();
        _filterUnknown.OnToggled   += _ => RebuildCannonButtons();
    }

    // ── Update state ───────────────────────────────────────────────────────

    public void UpdateState(GunneryConsoleBoundUserInterfaceState state)
    {
        _noServerLabel.Visible = false;
        _radarControl.Visible = true;
        if (!state.HasServer)
        {
            _noServerLabel.Visible = true;
            _radarControl.Visible = false;
            _radarControl.SelectedCannons.Clear();
            RebuildCannonButtons();
            return;
        }

        _radarControl.UpdateState(state);

        var newCannons = state.Cannons;

        // Determine whether the set of cannons changed or only their status did.
        var setChanged = newCannons.Count != _cannons.Count;
        if (!setChanged)
        {
            for (var i = 0; i < newCannons.Count; i++)
            {
                if (newCannons[i].Entity != _cannons[i].Entity)
                {
                    setChanged = true;
                    break;
                }
            }
        }

        _cannons = newCannons;

        if (setChanged)
        {
            RebuildCannonButtons();
        }
        else
        {
            // Only status changed — update button labels and colors in-place.
            foreach (var cannon in _cannons)
            {
                if (!_cannonButtons.TryGetValue(cannon.Entity, out var btn))
                    continue;
                btn.Text = GetCannonLabel(cannon);
                ApplyButtonColor(btn, cannon);
            }
        }

        // Guidance indicator.
        _guidanceLabel.Text = state.TrackedGuidedProjectile != null
            ? "GUIDANCE ACTIVE"
            : string.Empty;
    }

    // ── Cannon button list ─────────────────────────────────────────────────

    private void RebuildCannonButtons()
    {
        _cannonContainer.RemoveAllChildren();
        _cannonButtons.Clear();

        foreach (var cannon in _cannons)
        {
            if (!PassesFilter(cannon))
                continue;

            var btn = new Button
            {
                Text        = GetCannonLabel(cannon),
                ToggleMode  = true,
                Pressed     = _radarControl.SelectedCannons.Contains(cannon.Entity),
                HorizontalExpand = true,
                MinHeight   = 24,
            };
            ApplyButtonColor(btn, cannon);

            var captured = cannon;
            btn.OnToggled += args =>
            {
                if (args.Pressed)
                    _radarControl.SelectedCannons.Add(captured.Entity);
                else
                    _radarControl.SelectedCannons.Remove(captured.Entity);
            };

            _cannonButtons[cannon.Entity] = btn;
            _cannonContainer.AddChild(btn);
        }
    }

    private bool PassesFilter(CannonBlipData cannon)
    {
        return cannon.Category switch
        {
            CannonCategory.Energy    => _filterEnergy.Pressed,
            CannonCategory.Rocket    => _filterRocket.Pressed,
            CannonCategory.Ballistic => _filterBallistic.Pressed,
            _                        => _filterUnknown.Pressed,
        };
    }

    private void SyncButtonSelectionToRadarSelection()
    {
        foreach (var (entity, btn) in _cannonButtons)
            btn.Pressed = _radarControl.SelectedCannons.Contains(entity);
    }

    // ── Cannon state colors (single source of truth — must match XAML legend) ──

    private static readonly Color ColorReady    = Color.FromHex("#44FF44");
    private static readonly Color ColorCooldown = Color.FromHex("#FF4444");
    private static readonly Color ColorNoAmmo   = Color.FromHex("#FF8800");

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Applies the correct text color to a cannon button based on its state.</summary>
    private static void ApplyButtonColor(Button btn, CannonBlipData cannon)
    {
        btn.Label.FontColorOverride = cannon.CooldownSeconds > 0f ? ColorCooldown
                                    : cannon.HasAmmo             ? ColorReady
                                                                 : ColorNoAmmo;
    }

    private static string GetCannonLabel(CannonBlipData cannon)
    {
        return cannon.Name;
    }
}


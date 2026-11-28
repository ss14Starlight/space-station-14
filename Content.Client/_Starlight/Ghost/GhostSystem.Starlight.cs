// ReSharper disable CheckNamespace
using Content.Client._Starlight.Light;
using Content.Client.Administration.Systems;
using Content.Shared._Starlight.Administration.Components;
using Content.Shared._Starlight.Administration.Events;
using Content.Shared.Ghost;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;


namespace Content.Client.Ghost;

public sealed partial class GhostSystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private AdminSystem _adminSystem = default!;
    /// <summary>
    /// Whether Half Bright is on, the lighting step between personal light and fullbright.
    /// </summary>
    public bool HalfBright => _overlayManager.HasOverlay<HalfBrightOverlay>();

    public override void Shutdown()
    {
        base.Shutdown();
        SetHalfBright(false);
    }

    public void SetHalfBright(bool enabled)
    {
        if (enabled)
        {
            if (!HalfBright)
                _overlayManager.AddOverlay(new HalfBrightOverlay());
        }
        else if (_overlayManager.TryGetOverlay<HalfBrightOverlay>(out var overlay))
        {
            // Removing an overlay doesn't dispose it, and this one holds a render target.
            _overlayManager.RemoveOverlay(overlay);
            overlay.Dispose();
        }
    }

    /// <summary>
    /// Returns false to leave the other lighting steps to <see cref="OnToggleLighting"/>.
    /// </summary>
    private bool TryToggleHalfBright(EntityUid uid, EyeComponent eye, PointLightComponent? light, ToggleLightingActionEvent args)
    {
        if (HalfBright)
        {
            SetHalfBright(false);
            Popup.PopupEntity(Loc.GetString("ghost-gui-toggle-lighting-manager-popup-fullbright"), args.Performer);
            _contentEye.RequestEye(eye.DrawFov, false);
        }
        else if (eye.DrawLight && (light == null || light.Enabled))
        {
            if (light != null)
                _pointLightSystem.SetEnabled(uid, false, light);

            SetHalfBright(true);
            Popup.PopupEntity(Loc.GetString("ghost-gui-toggle-lighting-manager-popup-half-bright"), args.Performer);
        }
        else
        {
            return false;
        }

        args.Handled = true;
        return true;
    }
    partial void InitializeStarlight()
    {
        SubscribeLocalEvent<ToggleAGhostAdminOverlayEvent>(OnToggleAGhostAdminOverlay);
    }

    private void OnToggleAGhostAdminOverlay(ToggleAGhostAdminOverlayEvent args) // Toggles admin hud as a aghost action.
    {
        if (args.Handled ||
            args.Performer != _playerManager.LocalEntity ||
            !HasComp<AdminGhostComponent>(args.Performer))
        {
            return;
        }
        _adminSystem.ToggleAdminOverlay();
        args.Handled = true;
    }
}

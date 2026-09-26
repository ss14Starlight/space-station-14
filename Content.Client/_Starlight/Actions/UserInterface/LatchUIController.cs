using System.Numerics;
using Content.Client._Starlight.Actions.UI;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Starlight.Actions.Components;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Actions.UserInterface;

/// <summary>
/// Latch progress banner, positioned each frame to stay clear of both the
/// latcher and the target.
/// </summary>
[UsedImplicitly]
public sealed partial class LatchUIController : UIController
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Gap in tiles between a body's origin and the panel's nearest edge.
    /// </summary>
    private const float BodyClearance = 0.75f;

    /// <summary>
    /// Room in tiles needed above before the panel moves back up from below.
    /// Prevents flickering at the top of the viewport.
    /// </summary>
    private const float FlipBackHysteresis = 1.0f;

    private LatchStatusControl? _control;
    private LayoutContainer? _viewport;
    private bool _placedBelow;
    private SharedTransformSystem? _transform;
    private ActionsSystem? _actions;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        var viewport = UIManager.ActiveScreen?.FindControl<LayoutContainer>("ViewportContainer");
        if (viewport is null)
            return;

        _transform ??= _entities.System<SharedTransformSystem>();
        _actions ??= _entities.System<ActionsSystem>();

        _viewport = viewport;
        _control = new LatchStatusControl();
        _control.BiteHarderPressed += OnBiteHarderPressed;
        viewport.AddChild(_control);
    }

    private void OnScreenUnload()
    {
        _control?.BiteHarderPressed -= OnBiteHarderPressed;

        _control?.Orphan();
        _control = null;
        _viewport = null;
    }

    private void OnBiteHarderPressed()
    {
        if (_actions is null || _player.LocalEntity is not { } local)
            return;

        if (!_entities.TryGetComponent<LatchComponent>(local, out var latchComp))
            return;

        // Same as clicking the action in the hotbar - find the granted
        // BiteHarder action entity and trigger it directly.
        foreach (var action in _actions.GetActions(local))
        {
            if (!_entities.TryGetComponent<MetaDataComponent>(action, out var metadata)
                || metadata.EntityPrototype?.ID != latchComp.BiteHarderAction.Id)
                continue;

            _actions.TriggerAction(action);
            return;
        }
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        if (_control is null || _transform is null)
            return;

        if (_player.LocalEntity is not { } local)
        {
            _control.Hide();
            return;
        }

        string instruction;
        TimeSpan endTime, maxEndTime, maxDuration;
        bool isLatcher;
        EntityUid? partner;

        // As the latcher.
        if (_entities.TryGetComponent<LatchComponent>(local, out var latchComp) && latchComp.Active)
        {
            instruction = Loc.GetString("latch-instruction-latcher");
            endTime = latchComp.EndTime;
            maxEndTime = latchComp.MaxEndTime;
            maxDuration = latchComp.MaxDuration;
            isLatcher = true;
            partner = latchComp.Target;
        }
        // As the target.
        else if (_entities.TryGetComponent<LatchedComponent>(local, out var latchedComp) &&
                    _entities.TryGetComponent<LatchComponent>(latchedComp.Latcher, out var latcherComp) &&
                    latcherComp.Active)
        {
            instruction = Loc.GetString("latch-instruction-latchtarget");
            endTime = latcherComp.EndTime;
            maxEndTime = latcherComp.MaxEndTime;
            maxDuration = latcherComp.MaxDuration;
            isLatcher = false;
            partner = latchedComp.Latcher;
        }
        else
        {
            _control.Hide();
            _placedBelow = false;
            return;
        }

        var eyeMap = _eyeManager.CurrentEye.Position.MapId;
        if (!_entities.TryGetComponent<TransformComponent>(local, out var xform) ||
            xform.MapID != eyeMap)
        {
            _control.Hide();
            _placedBelow = false;
            return;
        }

        var fraction = GetFraction(endTime, maxDuration);
        var maxFraction = GetFraction(maxEndTime, maxDuration);
        _control.UpdateState(fraction, maxFraction, instruction, isLatcher);

        PlaceControl(xform, partner, eyeMap);
    }

    /// <summary>
    /// Places the panel above both bodies, or below them if there's no room above.
    /// </summary>
    private void PlaceControl(TransformComponent xform, EntityUid? partner, MapId eyeMap)
    {
        if (_control is null || _transform is null)
            return;

        var uiScale = UIManager.RootControl.UIScale;
        var localWorld = _transform.GetWorldPosition(xform);
        var localScreen = _eyeManager.WorldToScreen(localWorld) / uiScale;

        // Screen pixels per tile. Uses length so eye rotation doesn't affect it.
        var tilePixels = ((_eyeManager.WorldToScreen(localWorld + Vector2.UnitX) / uiScale) - localScreen).Length();

        var top = localScreen.Y;
        var bottom = localScreen.Y;

        if (partner is { } other
            && _entities.TryGetComponent<TransformComponent>(other, out var partnerXform)
            && partnerXform.MapID == eyeMap)
        {
            var partnerScreen = _eyeManager.WorldToScreen(_transform.GetWorldPosition(partnerXform)) / uiScale;
            top = MathF.Min(top, partnerScreen.Y);
            bottom = MathF.Max(bottom, partnerScreen.Y);
        }

        var clearance = BodyClearance * tilePixels;
        var aboveY = top - clearance - _control.Height;
        var belowY = bottom + clearance;

        var fitsBelow = _viewport is null || belowY + _control.Height <= _viewport.Height;

        if (_placedBelow)
        {
            if (aboveY >= FlipBackHysteresis * tilePixels || !fitsBelow)
                _placedBelow = false;
        }
        else if (aboveY < 0f && fitsBelow)
        {
            _placedBelow = true;
        }

        var x = localScreen.X - (_control.Width / 2f);
        var y = _placedBelow ? belowY : aboveY;

        // Keeps the banner on screen, overlapping the bodies only if neither side fits.
        if (_viewport is not null)
        {
            x = Math.Clamp(x, 0f, MathF.Max(0f, _viewport.Width - _control.Width));
            y = Math.Clamp(y, 0f, MathF.Max(0f, _viewport.Height - _control.Height));
        }

        LayoutContainer.SetPosition(_control, new Vector2(x, y));
    }

    private float GetFraction(TimeSpan endTime, TimeSpan maxDuration)
    {
        if (maxDuration <= TimeSpan.Zero)
            return 0f;

        var remaining = endTime - _timing.CurTime;
        return Math.Clamp((float)(remaining / maxDuration), 0f, 1f);
    }
}

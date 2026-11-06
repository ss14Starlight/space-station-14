using System.Numerics;
using Content.Client._Starlight.Actions.EntitySystems;
using Content.Client._Starlight.Actions.UI;
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.EntitySystems;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
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
    [Dependency] private IInputManager _input = default!;

    private const float BodyClearance = 0.75f;

    private const float FlipBackHysteresis = 1.0f;

    // Bar shake on Bite Harder. Small, since K9s can bite every 0.75s.
    private static readonly TimeSpan _shakeDuration = TimeSpan.FromSeconds(0.2);
    private const float ShakeMagnitude = 1.5f;
    private const float ShakeFrequency = 60f;

    // How long to hold a press locally before giving up on the server's answer.
    private static readonly TimeSpan _pendingTimeout = TimeSpan.FromSeconds(1);

    private LatchStatusControl? _control;
    private LayoutContainer? _viewport;
    private bool _placedBelow;
    private SharedTransformSystem? _transform;
    private ActionsSystem? _actions;
    private LatchSystem? _latch;

    private TimeSpan _shakeEnd;

    // A press sent to the server but not yet answered. The cursor freezes
    // here immediately so the click feels instant, with a locally predicted grade.
    private bool _pending;
    private float _pendingCursor;
    private float _pendingZone;
    private LatchStruggleResult _pendingResult;
    private TimeSpan _pendingBaseline;
    private TimeSpan _pendingExpiry;

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

        if (_latch is null)
        {
            _latch = _entities.System<LatchSystem>();
            _latch.LocalTargetBitten += OnLocalTargetBitten;
        }

        _control = new LatchStatusControl();
        _control.BiteHarderPressed += OnBiteHarderPressed;
        _control.StrugglePressed += OnStrugglePressed;
        viewport.AddChild(_control);

        CommandBinds.Unregister<LatchUIController>();
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.LatchStruggle,
                new PointerInputCmdHandler(OnStruggleKey, ignoreUp: false, outsidePrediction: true))
            .Register<LatchUIController>();
    }

    private void OnScreenUnload()
    {
        if (_control is not null)
        {
            _control.BiteHarderPressed -= OnBiteHarderPressed;
            _control.StrugglePressed -= OnStrugglePressed;
        }

        _control?.Orphan();
        _control = null;
        _viewport = null;

        CommandBinds.Unregister<LatchUIController>();
    }

    /// <summary>
    /// Struggles on key down while the local player has the struggle bar.
    /// Returning false otherwise lets a shared key (Space by default) fall
    /// through to Jump.
    /// </summary>
    private bool OnStruggleKey(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entities.HasComponent<LatchStruggleComponent>(local))
            return false;

        if (args.State == BoundKeyState.Down)
            OnStrugglePressed();

        return true;
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

    private void OnLocalTargetBitten()
        => _shakeEnd = _timing.RealTime + _shakeDuration;

    private void OnStrugglePressed()
    {
        if (_latch is null || _pending || _player.LocalEntity is not { } local)
            return;

        if (!_entities.TryGetComponent<LatchStruggleComponent>(local, out var struggle) ||
            !_entities.TryGetComponent<LatchedComponent>(local, out var latched) ||
            !_entities.TryGetComponent<LatchComponent>(latched.Latcher, out var latch))
        {
            return;
        }

        var remainder = GetTickRemainder();
        var now = _timing.CurTime + remainder;
        if (struggle.Block != LatchStruggleBlock.None || now < struggle.SegmentStart)
            return;

        _pending = true;
        _pendingCursor = SharedLatchSystem.GetStruggleCursor(struggle, now);
        _pendingZone = struggle.ZoneCenter;
        _pendingResult = SharedLatchSystem.GradeStruggle(_pendingCursor, struggle.ZoneCenter, latch.StrugglePerfectWidth, latch.StruggleGoodWidth);
        _pendingBaseline = struggle.LastPressTime;
        _pendingExpiry = _timing.RealTime + _pendingTimeout;

        _latch.RequestStruggle((float) remainder.TotalSeconds);
    }

    /// <summary>
    /// How far this frame is past the current tick, so the cursor moves
    /// smoothly between the ticks the server works in.
    /// </summary>
    private TimeSpan GetTickRemainder()
    {
        var remainder = _timing.TickRemainder;
        if (remainder < TimeSpan.Zero)
            return TimeSpan.Zero;

        return remainder > _timing.TickPeriod ? _timing.TickPeriod : remainder;
    }

    private void UpdateStruggle(EntityUid local, LatchComponent latch)
    {
        if (_control is null)
            return;

        if (!_entities.TryGetComponent<LatchStruggleComponent>(local, out var struggle))
        {
            _pending = false;
            _control.HideStruggle();
            return;
        }

        // The server has answered (or never will); drop the local prediction.
        if (_pending && (struggle.LastPressTime != _pendingBaseline || _timing.RealTime > _pendingExpiry))
            _pending = false;

        var now = _timing.CurTime + GetTickRemainder();
        var shake = GetShakeOffset();

        _control.StruggleKey = _input.TryGetKeyBinding(ContentKeyFunctions.LatchStruggle, out var binding)
            ? binding.GetKeyString()
            : null;

        if (struggle.Block != LatchStruggleBlock.None)
        {
            _pending = false;
            _control.UpdateStruggle(struggle.ZoneCenter, latch.StrugglePerfectWidth, latch.StruggleGoodWidth,
                SharedLatchSystem.GetStruggleCursor(struggle, now), LatchStruggleResult.None, struggle.Block, false, shake);
            return;
        }

        if (_pending)
        {
            _control.UpdateStruggle(_pendingZone, latch.StrugglePerfectWidth, latch.StruggleGoodWidth,
                _pendingCursor, _pendingResult, LatchStruggleBlock.None, false, shake);
            return;
        }

        if (struggle.LastResult != LatchStruggleResult.None && now < struggle.LastPressTime + latch.StruggleResultDisplay)
        {
            _control.UpdateStruggle(struggle.LastZoneCenter, latch.StrugglePerfectWidth, latch.StruggleGoodWidth,
                struggle.LastPressPosition, struggle.LastResult, LatchStruggleBlock.None, false, shake);
            return;
        }

        _control.UpdateStruggle(struggle.ZoneCenter, latch.StrugglePerfectWidth, latch.StruggleGoodWidth,
            SharedLatchSystem.GetStruggleCursor(struggle, now), LatchStruggleResult.None, LatchStruggleBlock.None,
            now >= struggle.SegmentStart, shake);
    }

    private float GetShakeOffset()
    {
        var remaining = _shakeEnd - _timing.RealTime;
        if (remaining <= TimeSpan.Zero)
            return 0f;

        var falloff = (float) (remaining / _shakeDuration);
        return MathF.Sin((float) _timing.RealTime.TotalSeconds * ShakeFrequency) * ShakeMagnitude * falloff;
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
        LatchComponent? targetLatch = null;

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
            instruction = _entities.HasComponent<LatchStruggleComponent>(local)
                ? Loc.GetString("latch-instruction-latchtarget-struggle")
                : Loc.GetString("latch-instruction-latchtarget");
            endTime = latcherComp.EndTime;
            maxEndTime = latcherComp.MaxEndTime;
            maxDuration = latcherComp.MaxDuration;
            isLatcher = false;
            partner = latchedComp.Latcher;
            targetLatch = latcherComp;
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

        if (targetLatch is not null)
            UpdateStruggle(local, targetLatch);
        else
            _control.HideStruggle();

        PlaceControl(xform, partner, eyeMap);
    }

    private void PlaceControl(TransformComponent xform, EntityUid? partner, MapId eyeMap)
    {
        if (_control is null || _transform is null)
            return;

        var uiScale = UIManager.RootControl.UIScale;
        var viewportOffset = _viewport?.GlobalPosition ?? Vector2.Zero;
        var localWorld = _transform.GetWorldPosition(xform);
        var localScreen = (_eyeManager.WorldToScreen(localWorld) / uiScale) - viewportOffset;

        // Screen pixels per tile. Uses length so eye rotation doesn't affect it.
        var tilePixels = ((_eyeManager.WorldToScreen(localWorld + Vector2.UnitX) / uiScale) - viewportOffset - localScreen).Length();

        var top = localScreen.Y;
        var bottom = localScreen.Y;

        if (partner is { } other
            && _entities.TryGetComponent<TransformComponent>(other, out var partnerXform)
            && partnerXform.MapID == eyeMap)
        {
            var partnerScreen = (_eyeManager.WorldToScreen(_transform.GetWorldPosition(partnerXform)) / uiScale) - viewportOffset;
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

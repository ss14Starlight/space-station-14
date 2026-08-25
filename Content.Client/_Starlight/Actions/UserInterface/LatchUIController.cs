using System.Numerics;
using Content.Client._Starlight.Actions.UI;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Starlight.Actions.Components;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Actions.UserInterface;

/// <summary>
/// Latch progress banner that floats above the local player, tracked via
/// world-to-screen the same way speech bubbles are.
/// </summary>
[UsedImplicitly]
public sealed partial class LatchUIController : UIController
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float VerticalOffset = 1.0f;

    private LatchStatusControl? _control;
    private SharedTransformSystem? _transform;

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

        _control = new LatchStatusControl();
        viewport.AddChild(_control);
    }

    private void OnScreenUnload()
    {
        _control?.Orphan();
        _control = null;
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

        // As the latcher.
        if (_entities.TryGetComponent<LatchComponent>(local, out var latchComp) && latchComp.Active)
        {
            instruction = Loc.GetString("latch-instruction-latcher");
            endTime = latchComp.EndTime;
            maxEndTime = latchComp.MaxEndTime;
            maxDuration = latchComp.MaxDuration;
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
        }
        else
        {
            _control.Hide();
            return;
        }

        if (!_entities.TryGetComponent<TransformComponent>(local, out var xform) ||
            xform.MapID != _eyeManager.CurrentEye.Position.MapId)
        {
            _control.Hide();
            return;
        }

        var fraction = GetFraction(endTime, maxDuration);
        var maxFraction = GetFraction(maxEndTime, maxDuration);
        _control.UpdateState(fraction, maxFraction, instruction);

        var worldPos = _transform.GetWorldPosition(xform) + new Vector2(0, VerticalOffset);
        var uiScale = UIManager.RootControl.UIScale;
        var lowerCenter = _eyeManager.WorldToScreen(worldPos) / uiScale;
        var screenPos = lowerCenter - new Vector2(_control.Width / 2f, _control.Height);
        LayoutContainer.SetPosition(_control, screenPos);
    }

    private float GetFraction(TimeSpan endTime, TimeSpan maxDuration)
    {
        if (maxDuration <= TimeSpan.Zero)
            return 0f;

        var remaining = endTime - _timing.CurTime;
        return Math.Clamp((float)(remaining / maxDuration), 0f, 1f);
    }
}

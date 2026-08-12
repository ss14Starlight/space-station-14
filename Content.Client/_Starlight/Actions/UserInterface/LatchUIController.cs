using Content.Client._Starlight.Actions.UI;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Starlight.Actions.Components;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Actions.UserInterface;

/// <summary>
/// Shows a latch progress banner while the local player is on either side.
/// </summary>
[UsedImplicitly]
public sealed partial class LatchUIController : UIController
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private LatchStatusControl? _control;
    private Control? _topLeftCluster;

    private const float FallbackTopMargin = 48f;
    private const float ClusterGap = 12f;

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

        // Same "TopLeft" container as DefaultGameScreen.xaml (menu bar/vote/actions).
        _topLeftCluster = UIManager.ActiveScreen?.FindControl<Control>("TopLeft");

        _control = new LatchStatusControl();
        viewport.AddChild(_control);
        LayoutContainer.SetAnchorPreset(_control, LayoutContainer.LayoutPreset.CenterTop);
        RepositionControl();
    }

    private void OnScreenUnload()
    {
        _control?.Orphan();
        _control = null;
        _topLeftCluster = null;
    }

    /// <summary>
    /// Docks the banner below the top-left cluster's current height.
    /// </summary>
    private void RepositionControl()
    {
        if (_control is null)
            return;

        var margin = _topLeftCluster is { Height: > 0 } cluster
            ? cluster.Height + ClusterGap
            : FallbackTopMargin;
        LayoutContainer.SetMarginTop(_control, margin);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        if (_control is null)
            return;

        RepositionControl();

        if (_player.LocalEntity is not { } local)
        {
            _control.Hide();
            return;
        }

        // As the latcher.
        if (_entities.TryGetComponent<LatchComponent>(local, out var latchComp) && latchComp.Active)
        {
            var fraction = GetFraction(latchComp.EndTime, latchComp.MaxDuration);
            var maxFraction = GetFraction(latchComp.MaxEndTime, latchComp.MaxDuration);
            _control.UpdateState(fraction, maxFraction, "Bite harder to deal more damage and extend the latch duration!");
            return;
        }

        // As the target.
        if (_entities.TryGetComponent<LatchedComponent>(local, out var latchedComp) &&
            _entities.TryGetComponent<LatchComponent>(latchedComp.Latcher, out var latcherComp) &&
            latcherComp.Active)
        {
            var fraction = GetFraction(latcherComp.EndTime, latcherComp.MaxDuration);
            var maxFraction = GetFraction(latcherComp.MaxEndTime, latcherComp.MaxDuration);
            _control.UpdateState(fraction, maxFraction, "Harm the latcher to break free faster!");
            return;
        }

        _control.Hide();
    }

    private float GetFraction(TimeSpan endTime, TimeSpan maxDuration)
    {
        if (maxDuration <= TimeSpan.Zero)
            return 0f;

        var remaining = endTime - _timing.CurTime;
        return Math.Clamp((float)(remaining / maxDuration), 0f, 1f);
    }
}

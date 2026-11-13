using Content.Shared._Starlight.Sleepiness.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Sleepiness;

public sealed partial class LightSleepOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private LightSleepSystem _lightSleep = default!;
    [Dependency] private IGameTiming _timing = default!;

    private LightSleepSonarOverlay _sonarOverlay = default!;
    private EntityUid? _lastLocalEntity;

    public override void Initialize()
    {
        base.Initialize();
        _sonarOverlay = new LightSleepSonarOverlay();
    }

    public override void Shutdown()
    {
        RemoveSonarOverlay();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var localEntity = _player.LocalEntity;
        if (localEntity != _lastLocalEntity)
        {
            _lastLocalEntity = localEntity;
            RemoveSonarOverlay();
        }

        if (localEntity is not { } player)
            return;

        var shouldShow = _lightSleep.IsLightSleepActive(player);
        if (shouldShow)
        {
            if (!_overlayManager.HasOverlay<LightSleepSonarOverlay>())
                _overlayManager.AddOverlay(_sonarOverlay);
        }
        else
        {
            RemoveSonarOverlay();
        }
    }

    private void RemoveSonarOverlay()
    {
        if (_overlayManager.HasOverlay<LightSleepSonarOverlay>())
        {
            _overlayManager.RemoveOverlay(_sonarOverlay);
            _sonarOverlay.ResetSleepSession();
        }
    }
}

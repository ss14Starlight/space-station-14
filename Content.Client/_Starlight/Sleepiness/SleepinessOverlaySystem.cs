using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Starlight.Sleepiness;

public sealed partial class SleepinessOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private ILogManager _logManager = default!;

    private SleepinessOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new SleepinessOverlay();

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } player)
            return;

        if (_statusEffects.TryGetEffectsEndTimeWithComp<SleepinessStatusEffectComponent>(player, out _))
        {
            if (!_overlayManager.HasOverlay<SleepinessOverlay>())
            {
                _overlayManager.AddOverlay(_overlay);
            }
        }
        else if (_overlayManager.HasOverlay<SleepinessOverlay>() && _overlay.IsAtRest)
        {
            _overlayManager.RemoveOverlay(_overlay);
        }
    }
}

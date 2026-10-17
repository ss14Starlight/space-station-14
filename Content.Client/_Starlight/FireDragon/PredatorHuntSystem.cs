using Content.Shared._Starlight.FireDragon;
using Content.Shared.StatusEffectNew.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Starlight.FireDragon;

public sealed partial class PredatorHuntSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private EntityQuery<StatusEffectContainerComponent> _containerQuery;
    private EntityQuery<StatusEffectComponent> _effectQuery;
    private EntityQuery<PredatorHuntStatusEffectComponent> _huntQuery;
    private PredatorHuntOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _containerQuery = GetEntityQuery<StatusEffectContainerComponent>();
        _effectQuery = GetEntityQuery<StatusEffectComponent>();
        _huntQuery = GetEntityQuery<PredatorHuntStatusEffectComponent>();

        _overlay = new PredatorHuntOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _overlay.Effect = GetHuntEffect(_player.LocalEntity);
    }

    private PredatorHuntStatusEffectComponent? GetHuntEffect(EntityUid? target)
    {
        if (!_containerQuery.TryComp(target, out var container)
            || container.ActiveStatusEffects is not { } effects)
            return null;

        var entities = effects.ContainedEntities;
        for (var i = 0; i < entities.Count; i++)
        {
            var effect = entities[i];
            if (_effectQuery.HasComp(effect) && _huntQuery.TryComp(effect, out var hunt))
                return hunt;
        }

        return null;
    }
}

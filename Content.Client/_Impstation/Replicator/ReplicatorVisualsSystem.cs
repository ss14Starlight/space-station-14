// these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

using Content.Client.DamageState;
using Content.Shared._Impstation.Replicator;
using Content.Shared.CombatMode;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;

namespace Content.Client._Impstation.Replicator;

public sealed class ReplicatorVisualsSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<ReplicatorComponent, ToggleCombatActionEvent>(OnToggleCombat);
        SubscribeLocalEvent<ReplicatorComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    /// <summary>
    /// tell the entity to enable or disable their combat sprite
    /// </summary>
    private void OnToggleCombat(Entity<ReplicatorComponent> ent, ref ToggleCombatActionEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _appearance.OnChangeData(ent, sprite);
    }

    /// <summary>
    /// enable or disable the combat sprite
    /// </summary>
    private void OnAppearanceChange(Entity<ReplicatorComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
        if (!TryComp<CombatModeComponent>(ent, out var combat))
            return;
        if (!_sprite.LayerMapTryGet(ent.Owner, ReplicatorVisuals.Combat, out var layerIndex, false)
            || !_sprite.LayerMapTryGet(ent.Owner, DamageStateVisualLayers.Base, out var baseIndex, false))
            return;

        // make sure we can sync the frames
        if (!_sprite.TryGetLayer(ent.Owner, layerIndex, out var combatLayer, false)
            || !_sprite.TryGetLayer(ent.Owner, baseIndex, out var baseLayer, false))
            return;

        // turn on combat visuals if the mob is alive and in combat mode. otherwise turn them off
        _sprite.LayerSetVisible(ent.Owner, layerIndex, _mobState.IsAlive(ent) && combat.IsInCombatMode);
        // then sync them to the base animation
        _sprite.LayerSetAnimationTime(combatLayer, baseLayer.AnimationTime);
        combatLayer.AnimationFrame = baseLayer.AnimationFrame;
        combatLayer.AnimationTimeLeft = baseLayer.AnimationTimeLeft;
    }

    private void OnMobStateChanged(Entity<ReplicatorComponent> ent, ref MobStateChangedEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _appearance.OnChangeData(ent, sprite);
    }
}

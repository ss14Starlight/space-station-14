using Content.Server._Starlight.CosmicCult.Components;
using Content.Server.Actions;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Audio;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Warps;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Dynamics;

namespace Content.Server._Starlight.CosmicCult.EntitySystems;

public sealed partial class CosmicColossusSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobThresholdSystem _threshold = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throw = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private CosmicMalignEmpoweredRiftSystem _riftSystem = default!;
    [Dependency] private CosmicCorruptingSystem _corrupting = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CosmicColossusComponent, ComponentInit>(OnSpawn);
        SubscribeLocalEvent<CosmicColossusComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var colossusQuery = EntityQueryEnumerator<CosmicColossusComponent>();
        while (colossusQuery.MoveNext(out var ent, out var comp))
        {
            if (comp.Attacking && _timing.CurTime >= comp.AttackHoldTimer)
            {
                _appearance.SetData(ent, ColossusVisuals.Status, ColossusStatus.Alive);
                _appearance.SetData(ent, ColossusVisuals.Sunder, ColossusAction.Stopped);
                _transform.Unanchor(ent);
                comp.Attacking = false;
            }
            if (comp.Hibernating && _timing.CurTime >= comp.HibernationTimer)
            {
                _appearance.SetData(ent, ColossusVisuals.Status, ColossusStatus.Alive);
                _appearance.SetData(ent, ColossusVisuals.Hibernation, ColossusAction.Stopped);
                _transform.Unanchor(ent);
                _audio.PlayPvs(comp.ReawakenSfx, ent);
                comp.Hibernating = false;
                Spawn(comp.CultBigVfx, Transform(ent).Coordinates);
                if (!TryComp<DamageableComponent>(ent, out var damageable))
                    continue;
                _damage.TryChangeDamage(ent, damageable.Damage / 2 * -1, true);
            }
            if (comp.Timed && _timing.CurTime >= comp.DeathTimer)
            {
                if (!_threshold.TryGetThresholdForState(ent, MobState.Dead, out var damage))
                    return;
                DamageSpecifier dspec = new();
                dspec.DamageDict.Add("Heat", damage.Value);
                _damage.TryChangeDamage(ent, dspec, true);
            }
        }
    }

    private void OnSpawn(Entity<CosmicColossusComponent> ent, ref ComponentInit args) // I WANT THIS BIG GUY HURLED TOWARDS THE STATION
    {
        ent.Comp.DeathTimer = _timing.CurTime + ent.Comp.DeathWait;
        var station = _station.GetStationInMap(Transform(ent).MapID);
        if (station is { } stationUid)
        {
            var stationGrid = _station.GetLargestGrid((stationUid, null));
            if (stationGrid is not null)
                _throw.TryThrow(ent, Transform(stationGrid.Value).Coordinates, baseThrowSpeed: 30, null, 0, 0, false, false, false, false, false);
        }
        if (ent.Comp.Timed)
        {
            _actions.AddAction(ent, ref ent.Comp.EffigyPlaceActionEntity, ent.Comp.EffigyPlaceAction, ent);
            ent.Comp.EffigyRechargeTimer = null;
            Dirty(ent);
        }
        _actions.AddAction(ent, ref ent.Comp.HibernateActionEntity, ent.Comp.HibernateAction, ent);
    }

    private void OnMobStateChanged(Entity<CosmicColossusComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
        {
            // Restore the Colossus after being revived.
            _appearance.SetData(ent, ColossusVisuals.Status, ColossusStatus.Alive);
            _appearance.SetData(ent, ColossusVisuals.Hibernation, ColossusAction.Stopped);
            _appearance.SetData(ent, ColossusVisuals.Sunder, ColossusAction.Stopped);

            _ambientSound.SetAmbience(ent, true);

            // The Colossus normally flies.
            if (TryComp<PhysicsComponent>(ent, out var revivePhysComp))
                _physics.SetBodyStatus(ent, revivePhysComp, BodyStatus.OnGround, true);
            // This should be fucking Air, i think? since death makes it Onground. idfk yml has air, if this is air they get dragged easily. i give up on this it works.

            // Restore the Colossus' light.
            if (TryComp<PointLightComponent>(ent, out var light))
            {
                _pointLight.SetRadius(ent, 4f, light);
                _pointLight.SetEnergy(ent, 4f, light);
            }

            EnsureComp<WarpPointComponent>(ent);

            if (TryComp<CosmicCorruptingComponent>(ent, out var corrupting))
                _corrupting.Enable((ent.Owner, corrupting));

            // The Colossus screams as it re-emerges.
            _audio.PlayPvs(
                ent.Comp.ScreamSfx,
                ent,
                AudioParams.Default.WithVolume(15f));
            _popup.PopupCoordinates(
                Loc.GetString("ghost-role-colossus-revive"),
                Transform(ent).Coordinates,
                PopupType.Large);

            return;
        }

        if (!TryComp<PhysicsComponent>(ent, out var physComp))
            return;
        ent.Comp.Hibernating = false;
        ent.Comp.Attacking = false;
        _appearance.SetData(ent, ColossusVisuals.Status, ColossusStatus.Dead);
        _appearance.SetData(ent, ColossusVisuals.Hibernation, ColossusAction.Stopped);
        _appearance.SetData(ent, ColossusVisuals.Sunder, ColossusAction.Stopped);
        _ambientSound.SetAmbience(ent, false);
        _audio.PlayPvs(ent.Comp.DeathSfx, ent);
        _physics.SetBodyStatus(ent, physComp, BodyStatus.OnGround, true);
        _popup.PopupCoordinates(
            Loc.GetString("ghost-role-colossus-death"),
            Transform(ent).Coordinates,
            PopupType.Large);
        // Dim the Colossus' light while dead.
        if (TryComp<PointLightComponent>(ent, out var deadLight))
        {
            _pointLight.SetRadius(ent, 1.5f, deadLight);
            _pointLight.SetEnergy(ent, 0.25f, deadLight);
        }

        RemComp<WarpPointComponent>(ent);

        //Turn off corruption
        if (TryComp<CosmicCorruptingComponent>(ent, out var deathCorrupting))
            _corrupting.Disable((ent.Owner, deathCorrupting));
    }
}

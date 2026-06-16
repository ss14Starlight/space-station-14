using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Popups;
using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Server.Singularity.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Projectiles;
using Content.Shared.Singularity.Components;
using Content.Shared.Temperature.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Content.Server.Bible.Components;

namespace Content.Server._Starlight.CosmicCult.EntitySystems;

public sealed partial class CosmicRiftSystem : EntitySystem
{
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private EmitterSystem _emitter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private PopupSystem _popup = default!;

    private const float RiftSpawnAreaScale = 0.7f;
    private const int RiftSpawnAttempts = 25;
    private const int RiftPurgeDistanceThreshold = 10;
    private static readonly EntProtoId _defaultRiftPrototype = "CosmicMalignRift";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CosmicMalignRiftComponent, InteractHandEvent>(OnInteract);

        SubscribeLocalEvent<CosmicMalignRiftComponent, EventPurgeRiftDoAfter>(OnPurgeDoAfter);
        SubscribeLocalEvent<CosmicCultComponent, EventAbsorbRiftDoAfter>(OnAbsorbDoAfter);

        SubscribeLocalEvent<CosmicLambdaParticleSourceComponent, ActivateInWorldEvent>(OnActivated);
        SubscribeLocalEvent<CosmicLambdaParticleSourceComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<CosmicMalignRiftComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnActivated(Entity<CosmicLambdaParticleSourceComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!_doAfter.IsRunning(ent.Comp.DoAfterId))
            return;

        args.Handled = true;
    }
    private void OnPowerChanged(Entity<CosmicLambdaParticleSourceComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && _doAfter.IsRunning(ent.Comp.DoAfterId))
            _doAfter.Cancel(ent.Comp.DoAfterId);
    }

    public void SpawnRift(EntityUid grid)
        => SpawnRift(grid, _defaultRiftPrototype);

    public void SpawnRift(EntityUid grid, EntProtoId riftPrototype)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var xform = Transform(grid);

        var targetCoords = xform.Coordinates;
        var gridBounds = gridComp.LocalAABB.Scale(RiftSpawnAreaScale);

        for (var i = 0; i < RiftSpawnAttempts; i++)
        {
            var randomX = _rand.Next((int) gridBounds.Left, (int) gridBounds.Right);
            var randomY = _rand.Next((int) gridBounds.Bottom, (int) gridBounds.Top);

            var tile = new Vector2i(randomX, randomY);

            // no air-blocked areas.
            if (_atmosphere.IsTileSpace(grid, xform.MapUid, tile) ||
                _atmosphere.IsTileAirBlocked(grid, tile, mapGridComp: gridComp))
            {
                continue;
            }

            // don't spawn inside of solid objects
            var physQuery = GetEntityQuery<PhysicsComponent>();
            var valid = true;

            // TODO: This should be using static lookup.
            foreach (var ent in _mapSystem.GetAnchoredEntities(grid, gridComp, tile))
            {
                if (!physQuery.TryGetComponent(ent, out var body))
                    continue;
                if (body.BodyType != BodyType.Static ||
                    !body.Hard ||
                    (body.CollisionLayer & (int) CollisionGroup.Impassable) == 0)
                    continue;

                valid = false;
                break;
            }
            if (!valid)
                continue;

            var pos = _mapSystem.GridTileToLocal(grid, gridComp, tile);

            targetCoords = pos;
            break;
        }

        Spawn(riftPrototype, targetCoords);
    }

    private void OnInteract(Entity<CosmicMalignRiftComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || _doAfter.IsRunning(ent.Comp.DoAfterId))
        {
            _popup.PopupEntity(Loc.GetString("cosmiccult-rift-inuse"), args.User, args.User);
            return;
        }

        if (!TryComp<CosmicCultComponent>(args.User, out var cultist))
        {
            _popup.PopupEntity(Loc.GetString("cosmiccult-rift-invaliduser"), args.User, args.User);
            return;
        }

        if (cultist.CosmicEmpowered)
        {
            _popup.PopupEntity(Loc.GetString("cosmiccult-rift-alreadyempowered"), args.User, args.User);
            return;
        }

        if (cultist.WasEmpowered)
        {
            _popup.PopupEntity(Loc.GetString("cosmiccult-rift-wasempowered"), args.User, args.User);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cosmiccult-rift-beginabsorb"), args.User, args.User);
        var doargs = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.AbsorbTime,
            new EventAbsorbRiftDoAfter(),
            args.User,
            ent)
        {
            DistanceThreshold = ent.Comp.DistanceThreshold, Hidden = true, BreakOnDamage = true, BreakOnHandChange = true, BreakOnMove = true,
            MovementThreshold = ent.Comp.MovementThreshold,
        };
        _doAfter.TryStartDoAfter(doargs, out var doAfterId);
        ent.Comp.DoAfterId = doAfterId;
    }

    private void OnInteractUsing(Entity<CosmicMalignRiftComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<BibleComponent>(args.Used))
            return;

        if (!HasComp<BibleUserComponent>(args.User))
            return;

        args.Handled = true;

        if (_doAfter.IsRunning(ent.Comp.DoAfterId))
        {
            _popup.PopupEntity(Loc.GetString("cosmiccult-rift-inuse"), args.User, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.PurgeTime,
            new EventPurgeRiftDoAfter(),
            ent,
            ent)
        {
            NeedHand = true,
            BreakOnWeightlessMove = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnDamage = true,
            RequireCanInteract = true,
            DistanceThreshold = ent.Comp.DistanceThreshold,
            MovementThreshold = ent.Comp.MovementThreshold,
        };

        _popup.PopupEntity(Loc.GetString("cosmiccult-rift-bible-charging"), args.User, args.User);

        _doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId);
        ent.Comp.DoAfterId = doAfterId;
    }

    private void OnAbsorbDoAfter(Entity<CosmicCultComponent> uid, ref EventAbsorbRiftDoAfter args)
    {
        var comp = uid.Comp;
        if (args.Args.Target is not { } target || args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var tgtpos = Transform(target).Coordinates;
        _actions.AddAction(uid, ref uid.Comp.CosmicFragmentationActionEntity, uid.Comp.CosmicFragmentationAction, uid);
        Spawn(uid.Comp.AbsorbVFX, tgtpos);
        comp.ActionEntities.Add(uid.Comp.CosmicFragmentationActionEntity);
        comp.WasEmpowered = true;
        comp.CosmicEmpowered = true;
        comp.CosmicSiphonQuantity = 2;
        comp.CosmicGlareRange = 10;
        comp.CosmicGlareDuration = TimeSpan.FromSeconds(10);
        comp.CosmicGlareStun = TimeSpan.FromSeconds(1);
        comp.CosmicImpositionDuration = TimeSpan.FromSeconds(7.4);
        comp.CosmicBlankDuration = TimeSpan.FromSeconds(26);
        comp.CosmicBlankDelay = TimeSpan.FromSeconds(0.4);
        comp.Respiration = false;
        EnsureComp<PressureImmunityComponent>(args.User);
        EnsureComp<TemperatureImmunityComponent>(args.User);
        _popup.PopupCoordinates(
            Loc.GetString("cosmiccult-rift-absorb", ("NAME", Identity.Entity(args.Args.User, EntityManager))),
            Transform(args.Args.User).Coordinates,
            PopupType.MediumCaution);
        QueueDel(target);
    }

    private void OnPurgeDoAfter(Entity<CosmicMalignRiftComponent> ent, ref EventPurgeRiftDoAfter args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var tgtpos = Transform(ent).Coordinates;

        if (TryComp<EmitterComponent>(args.User, out var emitterComp))
            _emitter.PowerOff(args.User, emitterComp);

        _popup.PopupCoordinates(
            Loc.GetString("cosmiccult-rift-purge"),
            Transform(ent).Coordinates,
            PopupType.Medium);
        var purgeVFX = Spawn(ent.Comp.PurgeVFX, tgtpos);
        _audio.PlayPvs(ent.Comp.PurgeSFX, Transform(purgeVFX).Coordinates);
        _audio.PlayPvs(ent.Comp.BeamSFX, Transform(args.User).Coordinates);
        QueueDel(ent);
    }
}

using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Audio;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.CosmicCult;
public abstract partial class SharedDeconversionJailSystem : EntitySystem
{
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedPopupSystem PopUp = default!;

    [Dependency] private INetManager _netManager = default!;
    [Dependency] private SharedAnomalySystem _anomaly = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAmbientSoundSystem _ambientAudio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedExplosionSystem _explosion = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeconversionOublietteComponent, ActivateInWorldEvent>(OnActivated);
        SubscribeLocalEvent<DeconversionOublietteComponent, EntInsertedIntoContainerMessage>(OnEntityInserted);
        SubscribeLocalEvent<DeconversionOublietteComponent, StorageInteractAttemptEvent>(OnStorageInteraction);
        SubscribeLocalEvent<DeconversionOublietteComponent, StorageOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<DeconversionOublietteComponent, StorageCloseAttemptEvent>(OnCloseAttempt);

        SubscribeLocalEvent<DeconversionOublietteComponent, ConstructionInteractDoAfterEvent>(OnConstruction);
        SubscribeLocalEvent<DeconversionOublietteComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<DeconversionOublietteComponent, DeconversionJailDoAfter>(OnDoAfter);
        SubscribeLocalEvent<DeconversionOublietteComponent, ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<AnomalyComponent, OubliettePurgeAttemptEvent>(PurgeAnomInfection);
        SubscribeLocalEvent<CosmicCultComponent, OubliettePurgeAttemptEvent>(PurgeCosmicCult);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeconversionOublietteComponent>();

        while (query.MoveNext(out var ent, out var comp))
        {
            if (comp.OublietteState == OublietteStates.Cooldown && Timing.CurTime > comp.CooldownTime)
            {
                PopUp.PopupCoordinates(Loc.GetString("cosmic-oubliette-recharged"), Transform(ent).Coordinates, PopupType.Medium);
                _appearance.SetData(ent, OublietteVisuals.Contents, OublietteStates.Idle);
                _ambientAudio.SetAmbience(ent, false);
                comp.OublietteState = OublietteStates.Idle;
                comp.CanInteract = true;
                Dirty(ent, comp);
            }
            if (comp.EjectContents && !_doAfter.IsRunning(comp.DoAfterId))
            {
                comp.EjectContents = false;
                comp.CanInteract = true;
                comp.OublietteState = OublietteStates.Idle;
                Dirty(ent, comp);
                PopUp.PopupCoordinates(Loc.GetString("cosmic-oubliette-eject"), Transform(ent).Coordinates, PopupType.SmallCaution);
                _appearance.SetData(ent, OublietteVisuals.Contents, OublietteStates.Idle);
                _ambientAudio.SetAmbience(ent, false);
                _storage.OpenStorage(ent);
            }
        }
    }

    private void OnPowerChanged(Entity<DeconversionOublietteComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && _doAfter.IsRunning(ent.Comp.DoAfterId))
        {
            _doAfter.Cancel(ent.Comp.DoAfterId);
            _ambientAudio.SetAmbience(ent, false);
            _appearance.SetData(ent, OublietteVisuals.Contents, OublietteStates.Idle);
            ent.Comp.OublietteState = OublietteStates.Idle;
            ent.Comp.EjectContents = true;
            ent.Comp.CanInteract = false;
            Dirty(ent);
        }
    }

    private void OnConstruction(Entity<DeconversionOublietteComponent> ent, ref ConstructionInteractDoAfterEvent args) =>
        _storage.OpenStorage(ent); // EJECT PLAYER BEFORE THEY GET DELETED

    private void OnEntityInserted(Entity<DeconversionOublietteComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_netManager.IsClient) // don't predict this function.
            return;
        if (ent.Comp.OublietteState == OublietteStates.Cooldown || !_power.IsPowered(ent.Owner) || !HasComp<HumanoidAppearanceComponent>(args.Entity))
            return;

        ent.Comp.CanInteract = false;
        var doAfterArgs = new DoAfterArgs(EntityManager, ent, ent.Comp.DeconversionTime, new DeconversionJailDoAfter(), ent, args.Entity)
        {
            NeedHand = false,
            BreakOnWeightlessMove = false,
            BreakOnMove = false,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            BreakOnDamage = false,
            RequireCanInteract = false,
        };

        _ambientAudio.SetAmbience(ent, true);
        PopUp.PopupCoordinates(Loc.GetString("cosmic-oubliette-activate"), Transform(ent).Coordinates, PopupType.Medium);
        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
        {
            _ambientAudio.SetAmbience(ent, false);
            ent.Comp.CanInteract = true;
            Dirty(ent);
            return;
        }
        _appearance.SetData(ent, OublietteVisuals.Contents, OublietteStates.Active);
        ent.Comp.OublietteState = OublietteStates.Active;
        ent.Comp.Victim = args.Entity;
        ent.Comp.DoAfterId = doAfterId;
        ent.Comp.EmoteTime = Timing.CurTime + Random.Next(ent.Comp.EmoteMinTime, ent.Comp.EmoteMaxTime);
        Dirty(ent);
    }

    private void OnActivated(Entity<DeconversionOublietteComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        if (ent.Comp.CanInteract)
            return;

        args.Handled = true;
    }

    private void OnStorageInteraction(Entity<DeconversionOublietteComponent> ent, ref StorageInteractAttemptEvent args)
    {
        if (ent.Comp.CanInteract)
            return;

        args.Cancelled = true;
    }

    private void OnOpenAttempt(Entity<DeconversionOublietteComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (ent.Comp.CanInteract)
            return;

        args.Cancelled = true;
    }

    private void OnCloseAttempt(Entity<DeconversionOublietteComponent> ent, ref StorageCloseAttemptEvent args)
    {
        if (ent.Comp.CanInteract)
            return;

        args.Cancelled = true;
    }

    private void OnDoAfter(Entity<DeconversionOublietteComponent> ent, ref DeconversionJailDoAfter args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled || args.Target is null)
        {
            ent.Comp.EjectContents = true;
            ent.Comp.CanInteract = false;
            Dirty(ent);
            return;
        }

        ent.Comp.OublietteState = OublietteStates.Cooldown;
        ent.Comp.CooldownTime = Timing.CurTime + ent.Comp.CooldownWait;

        Dirty(ent);
        _storage.OpenStorage(ent);
        _ambientAudio.SetAmbience(ent, false);
        _appearance.SetData(ent, OublietteVisuals.Contents, OublietteStates.Cooldown);

        var attempt = new OubliettePurgeAttemptEvent(args.Target.Value, ent);
        RaiseLocalEvent(args.Target.Value, attempt, true);

        if (!attempt.Handled)
        {
            PopUp.PopupCoordinates(Loc.GetString("cosmic-oubliette-failure"), Transform(ent).Coordinates, PopupType.Medium);
            _explosion.TriggerExplosive(ent);
        }

        args.Handled = true;
    }

    private void PurgeAnomInfection(Entity<AnomalyComponent> ent, ref OubliettePurgeAttemptEvent args)
    {
        if (args.Handled)
            return;
        _anomaly.ChangeAnomalyHealth(args.Target, args.Oubliette.Comp.AnomalyPurgeHealth);
        OublietteSuccess(args.Oubliette, args.Target);

        args.Handled = true;
    }

    private void PurgeCosmicCult(Entity<CosmicCultComponent> ent, ref OubliettePurgeAttemptEvent args)
    {
        if (args.Handled)
            return;
        RemComp<CosmicCultComponent>(args.Target);
        OublietteSuccess(args.Oubliette, args.Target);

        args.Handled = true;
    }

    protected void OublietteSuccess(Entity<DeconversionOublietteComponent> ent, EntityUid target)
    {
        PopUp.PopupCoordinates(Loc.GetString("cosmic-oubliette-success"), Transform(ent).Coordinates, PopupType.Medium);
        _audio.PlayPvs(ent.Comp.PurgeSFX, ent);
        Spawn(ent.Comp.PurgeVFX, Transform(target).Coordinates);
    }

    private void OnExamined(Entity<DeconversionOublietteComponent> ent, ref ExaminedEvent args)
    {
        switch (ent.Comp.OublietteState)
        {
            case OublietteStates.Active:
                args.PushMarkup(Loc.GetString("cosmic-oubliette-examine-active"));
                break;
            case OublietteStates.Cooldown:
                args.PushMarkup(Loc.GetString("cosmic-oubliette-examine-cooldown"));
                break;
            case OublietteStates.Idle:
                args.PushMarkup(Loc.GetString("cosmic-oubliette-examine-idle"));
                break;
            default:
                break;
        }
    }

    public sealed class OubliettePurgeAttemptEvent(EntityUid target, Entity<DeconversionOublietteComponent> oubliette) : HandledEntityEventArgs
    {
        public EntityUid Target = target;
        public Entity<DeconversionOublietteComponent> Oubliette = oubliette;
    }
}

[Serializable, NetSerializable]
public sealed partial class DeconversionJailDoAfter : SimpleDoAfterEvent;

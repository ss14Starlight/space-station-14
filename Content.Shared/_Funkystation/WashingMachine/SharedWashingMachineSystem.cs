using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared._Funkystation.Stains.Components;
using Content.Shared._Funkystation.Stains.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Funkystation.WashingMachine;

public abstract partial class SharedWashingMachineSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private SharedAudioSystem _audio = null!;
    [Dependency] private SharedPowerReceiverSystem _power = null!;
    [Dependency] private SharedEntityStorageSystem _storage = null!;
    [Dependency] private SharedAppearanceSystem _appearance = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DamageableSystem _damageable = null!;
    [Dependency] private ReactiveSystem _reactive = null!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedStainSystem _stains = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WashingMachineComponent, ActivateInWorldEvent>(OnActivate, before: [typeof(SharedEntityStorageSystem)]);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<WashingMachineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.State != WashingMachineState.Washing)
                continue;

            if (_timing.CurTime >= comp.WashFinishTime)
            {
                FinishWash(uid, comp);
                continue;
            }

            ProcessWashingHazards(uid, comp, frameTime);
        }
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<WashingMachineComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent.Owner, WashingMachineVisuals.State, ent.Comp.State);
    }

    [SubscribeLocalEvent]
    private void OnBreak(Entity<WashingMachineComponent> ent, ref BreakageEventArgs args)
    {
        ent.Comp.State = WashingMachineState.Broken;
        ent.Comp.WashFinishTime = null;
        _audio.Stop(ent.Comp.AudioStream);
        Dirty(ent.Owner, ent.Comp);
        _appearance.SetData(ent.Owner, WashingMachineVisuals.State, WashingMachineState.Broken);
    }

    [SubscribeLocalEvent]
    private void OnStorageOpenAttempt(Entity<WashingMachineComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (ent.Comp.State != WashingMachineState.Idle)
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<WashingMachineComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanComplexInteract)
            return;

        if (ent.Comp.State != WashingMachineState.Idle || !_power.IsPowered(ent.Owner) || _storage.IsOpen(ent.Owner))
            return;

        if (!TryComp<EntityStorageComponent>(ent, out var storage) || storage.Contents.ContainedEntities.Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("washing-machine-start"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Act = () =>
            {
                if (_timing.CurTime < ent.Comp.NextWashAllowed)
                {
                    _popup.PopupEntity(Loc.GetString("washing-machine-cooldown"), ent.Owner, user);
                    return;
                }
                TryStartWash(ent, user);
            }
        });
    }

    private void OnActivate(Entity<WashingMachineComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (ent.Comp.State != WashingMachineState.Idle || !_power.IsPowered(ent.Owner) || _storage.IsOpen(ent.Owner))
            return;

        if (!TryComp<EntityStorageComponent>(ent, out var storage) || storage.Contents.ContainedEntities.Count == 0)
            return;

        if (_timing.CurTime < ent.Comp.NextWashAllowed)
        {
            _popup.PopupEntity(Loc.GetString("washing-machine-cooldown"), ent.Owner, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = true;
        TryStartWash(ent, args.User);
    }

    private void ProcessWashingHazards(EntityUid uid, WashingMachineComponent comp, float frameTime)
    {
        if (!TryComp<EntityStorageComponent>(uid, out var storage) || storage.Contents.ContainedEntities.Count == 0)
            return;

        var damageProto = _proto.Index(comp.WashingDamageType);
        var damage = new DamageSpecifier(damageProto, comp.BluntDamagePerSecond * frameTime);

        var waterSpray = new Solution();
        waterSpray.AddReagent(comp.WaterSprayReagent, comp.WaterSprayAmount);

        var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(uid));

        var sprayWater = rand.Prob(comp.WaterSprayChance * frameTime);

        var hasHeavyItems = false;

        foreach (var item in storage.Contents.ContainedEntities)
        {
            _damageable.TryChangeDamage(item, damage, true);

            if (sprayWater)
                _reactive.DoEntityReaction(item, waterSpray, ReactionMethod.Touch);

            if (!hasHeavyItems && !HasComp<ClothingComponent>(item))
                hasHeavyItems = true;
        }

        if (hasHeavyItems)
        {
            if (rand.Prob(comp.ThumpSoundChance * frameTime))
                _audio.PlayPredicted(comp.HitSound, uid, uid);

            comp.AccumulatedSelfDamage += comp.SelfDamagePerSecond * frameTime;
        }
    }

    private void FinishWash(EntityUid uid, WashingMachineComponent comp)
    {
        comp.State = WashingMachineState.Idle;
        comp.WashFinishTime = null;
        comp.NextWashAllowed = _timing.CurTime + comp.Cooldown;

        _audio.Stop(comp.AudioStream);
        _audio.PlayPredicted(comp.WashFinishedSound, uid, uid);
        _appearance.SetData(uid, WashingMachineVisuals.State, WashingMachineState.Idle);

        HashSet<EntityUid> items = new();
        if (TryComp<EntityStorageComponent>(uid, out var storage))
        {
            items = storage.Contents.ContainedEntities.ToHashSet();

            // Clean off prints and DNA
            UpdateForensics((uid, comp), items);

            foreach (var item in items)
            {
                if (TryComp<StainableComponent>(item, out var stain) && _solution.TryGetSolution(item, stain.SolutionName, out var sol))
                {
                    _solution.RemoveAllSolution(sol.Value);
                    _stains.UpdateVisuals((item, stain));
                }
            }
        }

        var machineEv = new WashingMachineFinishedWashingEvent(items);
        RaiseLocalEvent(uid, ref machineEv);

        var itemEv = new WashingMachineWashedEvent(uid, items);
        foreach (var item in items)
        {
            RaiseLocalEvent(item, ref itemEv);
        }

        if (comp.AccumulatedSelfDamage > 0)
        {
            var damageProto = _proto.Index(comp.WashingDamageType);
            var selfDamage = new DamageSpecifier(damageProto, comp.AccumulatedSelfDamage);
            _damageable.TryChangeDamage(uid, selfDamage, ignoreResistances: true);
            comp.AccumulatedSelfDamage = 0;
        }

        Dirty(uid, comp);
        _storage.OpenStorage(uid);
    }

    private void TryStartWash(Entity<WashingMachineComponent> ent, EntityUid user)
    {
        if (ent.Comp.State != WashingMachineState.Idle || !_power.IsPowered(ent.Owner) || _storage.IsOpen(ent.Owner))
            return;

        if (_timing.CurTime < ent.Comp.NextWashAllowed)
            return;

        if (!TryComp<EntityStorageComponent>(ent, out var storage) || storage.Contents.ContainedEntities.Count == 0)
            return;

        ent.Comp.State = WashingMachineState.Washing;
        ent.Comp.WashFinishTime = _timing.CurTime + ent.Comp.WashTime;

        Dirty(ent.Owner, ent.Comp);
        _appearance.SetData(ent.Owner, WashingMachineVisuals.State, WashingMachineState.Washing);

        var items = storage.Contents.ContainedEntities.ToHashSet();

        var machineEv = new WashingMachineStartedWashingEvent(items);
        RaiseLocalEvent(ent.Owner, ref machineEv);

        var itemEv = new WashingMachineIsBeingWashed(ent.Owner, items);
        foreach (var item in items)
        {
            RaiseLocalEvent(item, ref itemEv);
        }

        ent.Comp.AudioStream = _audio.PlayPredicted(ent.Comp.WashLoopSound, ent.Owner, ent.Owner)?.Entity;
    }

    protected virtual void UpdateForensics(Entity<WashingMachineComponent> ent, HashSet<EntityUid> items)
    {
    }
}

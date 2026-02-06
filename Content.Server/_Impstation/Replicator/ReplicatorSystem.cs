// these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

using Content.Server._Impstation.Administration.Components;
using Content.Server.Actions;
using Content.Server.Emp;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Impstation.SpawnedFromTracker;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Pinpointer;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Impstation.Replicator;

public sealed class ReplicatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PinpointerSystem _pinpointer = default!;
    [Dependency] private readonly MindSystem  _mind = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<ReplicatorComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ReplicatorComponent, ToggleCombatActionEvent>(OnCombatToggle);
        SubscribeLocalEvent<ReplicatorComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawnerUsed);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorSpawnNestActionEvent>(OnSpawnNestAction);
        SubscribeLocalEvent<ReplicatorComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<ReplicatorComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMindRemoved(Entity<ReplicatorComponent> ent, ref MindRemovedMessage args)
    {
        // remove all the actions when the mind is removed.
        foreach (var action in ent.Comp.Actions)
        {
            QueueDel(action);
        }
    }

    private void OnSpawnNestAction(Entity<ReplicatorComponent> ent, ref ReplicatorSpawnNestActionEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var xform = Transform(ent);
        var coords = xform.Coordinates;

        if (!coords.IsValid(EntityManager) || xform.MapID == MapId.Nullspace)
            return;

        // spawn a nest, then make sure it has ReplicatorNestComponent
        var myNest = Spawn("ReplicatorNest", xform.Coordinates);
        var myNestComp = EnsureComp<ReplicatorNestComponent>(myNest);

        var netEnt = GetNetEntity(ent);

        // add ourselves to the list of related replicators if the nest hasn't been destroyed (and therefore there are no orphaned replicators)
        if (ent.Comp.RelatedReplicators.Count <= 0 || ent.Comp.Queen && !ent.Comp.RelatedReplicators.Contains(netEnt))
            ent.Comp.RelatedReplicators.Add(netEnt);

        // then set that nest's spawned minions to our saved list of related replicators.
        // while we're in here, we might as well update all their pinpointers.
        HashSet<EntityUid> newMinions = [];
        foreach (var netId in ent.Comp.RelatedReplicators)
        {
            var uid = GetEntity(netId);
            if (!TryComp<ReplicatorComponent>(uid, out var comp))
                continue;

            newMinions.Add(uid);

            if (!_inventory.TryGetSlotEntity(uid, "pocket1", out var pocket1) || !TryComp<PinpointerComponent>(pocket1, out var pinpointer))
                continue;
            // set the target to the nest
            _pinpointer.SetTarget(pocket1.Value, myNest, pinpointer);

            comp.MyNest = GetNetEntity(myNest);
        }
        myNestComp.SpawnedMinions = newMinions;
        // make sure the nest knows who we are, and vice versa.
        myNestComp.SpawnedMinions.Add(ent);
        ent.Comp.MyNest = GetNetEntity(myNest);
        // and we don't need the RelatedReplicators list anymore, so,
        ent.Comp.RelatedReplicators.Clear();

        // remove queen status from this replicator
        ent.Comp.Queen = false;

        // remove the Crown
        if (HasComp<ReplicatorSignComponent>(ent))
            RemComp<ReplicatorSignComponent>(ent);

        UpgradeReplicator(ent, ent.Comp.FirstStage);

        // then we need to remove the action, to ensure it can't be used infinitely.
        QueueDel(args.Action);
    }

    private void OnGhostRoleSpawnerUsed(Entity<ReplicatorComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<SpawnedFromTrackerComponent>(args.Spawner, out var tracker) || !TryComp<ReplicatorNestComponent>(tracker.SpawnedFrom, out var nestComp))
            return;
        // add the spawned replicator to the nest's list when someone takes the ghostrole.
        nestComp.SpawnedMinions.Add(ent);
        // then remove the spawner from the nest's list of unclaimed spawners.
        nestComp.UnclaimedSpawners.Remove(args.Spawner);

        // tell the new fella who they momma is
        ent.Comp.MyNest = GetNetEntity(tracker.SpawnedFrom);
    }

    public Entity<ReplicatorComponent>? UpgradeReplicator(Entity<ReplicatorComponent> ent, ProtoId<PolymorphPrototype> nextStage)
    {
        if (!_mind.TryGetMind(ent, out var mind, out _))
            return null;

        if (_polymorph.PolymorphEntity(ent, nextStage) is not { } upgraded)
            return null;

        var upgradedComp = EnsureComp<ReplicatorComponent>(upgraded);
        upgradedComp.RelatedReplicators = ent.Comp.RelatedReplicators;
        upgradedComp.MyNest = ent.Comp.MyNest;
        var nestUid = GetEntity(ent.Comp.MyNest);

        if (ent.Comp.MyNest is { } nest && TryComp<ReplicatorNestComponent>(nestUid, out var nestComp))
        {
            nestComp.SpawnedMinions.Remove(ent);
            nestComp.SpawnedMinions.Add(upgraded);

            _audio.PlayPvs(nestComp.UpgradeSound, upgraded);
        }

        Dirty(upgraded, upgradedComp);
        _popup.PopupEntity(Loc.GetString($"{ent.Comp.ReadyToUpgradeMessage}-self"), upgraded, PopupType.Medium);

        return (upgraded, upgradedComp);
    }

    private void OnAttackAttempt(Entity<ReplicatorComponent> ent, ref AttackAttemptEvent args)
    {
        // Can't attack your friends.
        if (HasComp<ReplicatorComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("replicator-on-replicator-attack-fail"), ent, ent, PopupType.MediumCaution);
            args.Cancel();
        }

        // Can't attack the nest.
        if (HasComp<ReplicatorNestComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("replicator-on-nest-attack-fail"), ent, ent, PopupType.MediumCaution);
            args.Cancel();
        }
    }

    private void OnCombatToggle(Entity<ReplicatorComponent> ent, ref ToggleCombatActionEvent args)
    {
        if (!TryComp<CombatModeComponent>(ent, out var combat))
            return;

        // visual indicator that the replicator is aggressive.
        _appearance.SetData(ent, ReplicatorVisuals.Combat, combat.IsInCombatMode);
    }

    private void OnMobStateChanged(Entity<ReplicatorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical | args.NewMobState != MobState.Dead)
            return;

        _appearance.SetData(ent, ReplicatorVisuals.Combat, false);

        if (!HasComp<ReplicatorSignComponent>(ent))
            return;

        foreach (var netId in ent.Comp.RelatedReplicators)
        {
            var uid = GetEntity(netId);
            if (!TryComp<ReplicatorComponent>(uid, out var comp))
                continue;
            _popup.PopupEntity(Loc.GetString(comp.QueenDiedMessage), uid, uid, PopupType.LargeCaution);
        }
    }

    private void OnEmpPulse(Entity<ReplicatorComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;
        _stun.TryAddParalyzeDuration(ent, ent.Comp.EmpStunTime);
    }
}

using System.Linq;
using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Changeling.Components;
using Content.Shared.Mind;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Store.Components;
using Content.Shared.Store;
using Content.Shared.RetractableItemAction;
using Content.Shared.UserInterface;
using Content.Shared._Starlight.Store.Events;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared._Starlight.Overlay.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private SharedEyeSystem _hiveEye = default!;
    [Dependency] private SharedViewSubscriberSystem _hiveViews = default!;
    [Dependency] private BlindableSystem _hiveBlindness = default!;
    [Dependency] private FlashImmunitySystem _hiveVision = default!;
    [Dependency] private SleepingSystem _hiveSleeping = default!;
    [Dependency] private NPCSystem _hiveNpc = default!;

    public void EnsureHive(EntityUid body)
    {
        if (HasComp<ChangelingColonyMemberComponent>(body)
            || !_mind.TryGetMind(body, out var mind, out _) || !TryComp<ChangelingComponent>(body, out var ling))
            return;
        var colony = EnsureComp<ChangelingColonyComponent>(mind);
        colony.Active = body;
        colony.Core = CreateMindspace(mind, mind, body, colony);
        var core = EnsureComp<ChangelingComponent>(colony.Core);
        core.Resources.Genetics = ling.Resources.Genetics;
        if (TryComp<ChangelingIdentityComponent>(body, out var identity))
            colony.Identities = identity.ConsumedIdentities;
        ShareHiveStore(body, colony.Core);
        EnsureComp<StoreComponent>(colony.Core).AccountOwner = mind;
        AddColonyMember(body, mind, colony);
        EnsureHiveAction(mind, "ActionChangelingColony");
    }

    private EntityUid CreateMindspace(EntityUid mind, EntityUid master, EntityUid body, ChangelingColonyComponent colony)
    {
        var anchor = Spawn("ChangelingMindspace", Transform(body).Coordinates);
        var observer = Comp<ChangelingMindspaceComponent>(anchor);
        observer.Colony = master;
        var member = EnsureComp<ChangelingColonyMemberComponent>(anchor);
        member.Mind = master;
        member.IsFragment = true;
        colony.Minds.Add(mind, anchor);
        _metaData.SetEntityName(anchor, Comp<MindComponent>(mind).CharacterName ?? Name(body));
        SetHiveUi(anchor);
        _language.AddLanguage(anchor, "ChangelingHive");
        ObserveHiveBody(anchor, observer, body);
        return anchor;
    }

    private void SetHiveUi(EntityUid body)
    {
        EnsureComp<UserInterfaceComponent>(body);
        _chemistryUi.SetUi(body, ChangelingColonyUiKey.Key,
            new InterfaceData("ChangelingColonyBoundUserInterface", interactionRange: 0, requireInputValidation: false));
    }

    private void ShareHiveStore(EntityUid source, EntityUid target)
    {
        if (!TryComp<StoreComponent>(source, out var store))
            return;
        var copy = EnsureComp<StoreComponent>(target);
        copy.Categories = store.Categories;
        copy.CurrencyWhitelist = store.CurrencyWhitelist;
        copy.Balance = store.Balance;
        copy.BalanceSpent = store.BalanceSpent;
        copy.FullListingsCatalog = store.FullListingsCatalog;
        copy.BoughtEntities = store.BoughtEntities;
        copy.AccountOwner = store.AccountOwner;
        copy.OwnerOnly = true;
    }

    private void PrepareHiveBody(EntityUid body, ChangelingColonyComponent colony)
    {
        if (TryComp<ChangelingColonyMemberComponent>(body, out var carrier) && !carrier.IsFragment)
            RemComp<HTNComponent>(body);
        if (TryComp<ChangelingColonyMemberComponent>(body, out var member) && member.SleepingWhileVacant)
        {
            _hiveSleeping.TryWaking((body, null), ignoreSsd: true);
        }
        var ling = EnsureComp<ChangelingComponent>(body);
        ling.Resources.Genetics = Comp<ChangelingComponent>(colony.Core).Resources.Genetics;
        ShareHiveStore(colony.Core, body);
        var identity = EnsureComp<ChangelingIdentityComponent>(body);
        foreach (var saved in identity.ConsumedIdentities)
        {
            if (!colony.Identities.Contains(saved))
                colony.Identities.Add(saved);
        }
        identity.ConsumedIdentities = colony.Identities;
        identity.SharedStorage = true;
        EnsureComp<ChangelingDevourComponent>(body);
        if (!Comp<ChangelingColonyMemberComponent>(body).IsFragment)
            EnsureComp<ChangelingTransformComponent>(body);
        if (ling.HasChemicalSting)
            InitializeStingChemistry(body);
        ApplyBodyEvolution(body, ling);
        Dirty(body, ling);
        Dirty(body, identity);
    }

    private void AbsorbHiveMind(EntityUid eater, EntityUid victim)
    {
        EnsureHive(eater);
        if (TryGetColony(victim, out _, out var existingHive))
        {
            RetireConsumedCarrier(victim, existingHive);
            return;
        }
        if (!TryGetColony(eater, out var member, out var colony)
            || !_mind.TryGetMind(victim, out var mind, out _)
            || mind == member.Mind || colony.Minds.ContainsKey(mind))
            return;
        if (TryComp<ActionsContainerComponent>(mind, out var oldActions))
        {
            foreach (var action in oldActions.Container.ContainedEntities.ToArray())
                QueueDel(action);
        }
        var anchor = CreateMindspace(mind, member.Mind, eater, colony);
        colony.PendingMinds.Add(mind);
        _mind.TransferTo(mind, anchor, ghostCheckOverride: true, createGhost: false);
        RequestHiveConsent(member.Mind, mind, anchor, colony);
    }

    private void RetireConsumedCarrier(EntityUid body, ChangelingColonyComponent colony)
    {
        if (_mind.TryGetMind(body, out var mind, out _) && colony.Minds.ContainsKey(mind))
            ReturnToHive(mind, colony);
        StopHiveBody(body);
        colony.Members.Remove(body);
        if (colony.Active == body)
            colony.Active = colony.Core;

        _transform.TryGetMapOrGridCoordinates(body, out var position);
        foreach (var anchor in colony.Minds.Values.Concat(colony.VacantAnchors))
        {
            if (TerminatingOrDeleted(anchor) || !TryComp<ChangelingMindspaceComponent>(anchor, out var observer))
                continue;
            if (observer.Viewing == body || Transform(anchor).ParentUid == body)
            {
                observer.Viewing = null;
                _hiveEye.SetTarget(anchor, null);
                if (position is { } coordinates)
                    _transform.SetCoordinates(anchor, coordinates);
                Dirty(anchor, observer);
            }
            if (observer.Listening == body)
                observer.Listening = null;
            UpdateHiveObserver(anchor, observer, colony);
            if (_chemistryUi.IsUiOpen(anchor, ChangelingColonyUiKey.Key))
                UpdateColonyUi(anchor, colony);
        }

        RemComp<StoreComponent>(body);
        RemComp<ChangelingColonyMemberComponent>(body);
    }

    private void EnsureHiveAction(EntityUid mind, string prototype)
    {
        EntityUid? action = null;
        if (TryComp<ActionsContainerComponent>(mind, out var actions))
        {
            var existing = actions.Container.ContainedEntities.FirstOrDefault(a => !TerminatingOrDeleted(a)
                && MetaData(a).EntityPrototype?.ID == prototype);
            if (existing.IsValid())
                action = existing;
        }
        action ??= _actionContainers.AddAction(mind, prototype);
        if (action is not { } actionUid || !TryComp<MindComponent>(mind, out var consciousness)
            || consciousness.OwnedEntity is not { } body || TerminatingOrDeleted(body))
            return;
        // A transferred mind can arrive before its actions are linked to the new carrier.
        if (_actions.GetAction(actionUid) is not { } current)
            return;
        if (current.Comp.AttachedEntity != body || !_actions.GetActions(body).Any(a => a.Owner == actionUid))
            _actions.GrantContainedAction(body, mind, actionUid);
        if (prototype is "ActionChangelingColony" or LivingBloodAction)
            _actions.SetEnabled(current.AsNullable(), true);
        if (prototype == LivingBloodAction && TryComp<ChangelingColonyComponent>(mind, out var colony))
            _actions.SetToggled(current.AsNullable(), colony.AnimateBlood);
    }

    private void SyncHiveActions(EntityUid master, EntityUid mind)
    {
        if (TryComp<ChangelingColonyComponent>(master, out var hive) && hive.PendingMinds.Contains(mind))
            return;
        EnsureHiveAction(mind, "ActionChangelingColony");
        if (mind == master && TryComp<ChangelingColonyComponent>(master, out var colony))
            EnsureLivingBloodAction(master, colony);
        if (mind == master || !TryComp<ActionsContainerComponent>(master, out var actions))
            return;
        foreach (var action in actions.Container.ContainedEntities.ToArray())
        {
            if (!TerminatingOrDeleted(action) && MetaData(action).EntityPrototype is { } prototype && prototype.ID != LivingBloodAction)
                EnsureHiveAction(mind, prototype.ID);
        }
    }

    private bool IsHiveMaster(EntityUid body, ChangelingColonyMemberComponent member)
        => _mind.TryGetMind(body, out var mind, out _) && mind == member.Mind;

    private bool IsHivePlayer(EntityUid body, ChangelingColonyComponent colony)
        => _mind.TryGetMind(body, out var mind, out _) && colony.Minds.ContainsKey(mind) && !colony.PendingMinds.Contains(mind);

    [SubscribeLocalEvent]
    private void OnHivePurchase(ref StorePurchaseAttemptEvent args)
    {
        if (HasComp<ChangelingMindspaceComponent>(args.Buyer)
            || TryGetColony(args.Buyer, out var member, out _) && (member.IsFragment || !IsHiveMaster(args.Buyer, member)))
            args.Cancel = true;
    }

    private bool ReturnToHive(EntityUid mind, ChangelingColonyComponent colony)
    {
        if (!colony.Minds.TryGetValue(mind, out var anchor) || TerminatingOrDeleted(anchor)
            || !TryComp<MindComponent>(mind, out var consciousness) || consciousness.OwnedEntity == anchor)
            return false;
        var body = consciousness.OwnedEntity;
        if (body is { } old && TryComp<ChangelingColonyMemberComponent>(old, out var member))
        {
            EndCapture(old, member);
            StopHiveBody(old);
        }
        _mind.TransferTo(mind, anchor, ghostCheckOverride: true, createGhost: false);
        SyncHiveActions(Comp<ChangelingMindspaceComponent>(anchor).Colony, mind);
        if (body is { } vacant && colony.Members.Contains(vacant) && !TerminatingOrDeleted(vacant))
            ActivateVacantCarrier(vacant);
        var observer = Comp<ChangelingMindspaceComponent>(anchor);
        if (body is { } view && colony.Members.Contains(view) && !TerminatingOrDeleted(view))
            ObserveHiveBody(anchor, observer, view);
        UpdateHiveObserver(anchor, observer, colony);
        RefreshFragmentActions(anchor, colony);
        UpdateColonyUi(anchor, colony);
        observer.OpenUiAt = _timing.CurTime + TimeSpan.FromMilliseconds(100);
        return true;
    }

    private void ActivateVacantCarrier(EntityUid body)
    {
        if (!TryComp<ChangelingColonyMemberComponent>(body, out var member) || !_mobState.IsAlive(body))
            return;
        if (!member.IsFragment)
        {
            RemComp<HTNComponent>(body);
            if (!HasComp<SleepingComponent>(body) && _hiveSleeping.TrySleeping((body, null)))
            {
                member.SleepingWhileVacant = true;
            }
            return;
        }
        if (!HasComp<HTNComponent>(body))
            AddComp(body, new HTNComponent { RootTask = new HTNCompoundTask { Task = "SimpleHostileCompound" } });
        _factionSystem.ClearFactions(body);
        _factionSystem.AddFaction(body, ColonyFaction);
        _hiveNpc.WakeNPC(body);
    }

    [SubscribeLocalEvent]
    private void OnHiveSleepChanged(Entity<ChangelingColonyMemberComponent> ent, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep || !ent.Comp.SleepingWhileVacant)
            return;
        ent.Comp.SleepingWhileVacant = false;
        if (!TryComp<ActionsContainerComponent>(ent, out var actions))
            return;
        foreach (var action in actions.Container.ContainedEntities)
        {
            if (!TerminatingOrDeleted(action) && MetaData(action).EntityPrototype?.ID == "ActionWake")
                QueueDel(action);
        }
    }

    private void StopHiveBody(EntityUid body)
    {
        if (TryComp<ChangelingMindspaceComponent>(body, out var observer))
            observer.OpenUiAt = TimeSpan.Zero;
        _chemistryUi.CloseUi(body, ChangelingColonyUiKey.Key);
        _chemistryUi.CloseUi(body, ChangelingChemistryUiKey.Key);
        _chemistryUi.CloseUi(body, StoreUiKey.Key);
        if (!TryComp<ChangelingComponent>(body, out var ling))
            return;
        if (ling.StrainedMusclesActive)
            ToggleStrainedMuscles(body, ling);
        if (ling.StealthEnabled)
            ToggleChameleonSkin(body, ling, false);
        StopAdaptations(body, ling);
        foreach (var action in _actions.GetActions(body).ToArray())
        {
            if (TryComp<RetractableItemActionComponent>(action.Owner, out var retractable))
                _retractable.Retract((action.Owner, retractable), body);
        }
        RemoveAllChangelingEquipment(body, ling);
    }

    [SubscribeLocalEvent]
    private void OnObserveHive(EntityUid uid, ChangelingColonyMemberComponent member, ChangelingObserveMessage args)
    {
        if (args.Actor != uid || !TryComp<ChangelingMindspaceComponent>(uid, out var observer)
            || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || !IsHivePlayer(uid, colony))
            return;
        var body = GetEntity(args.Carrier);
        if (colony.Members.Contains(body) && !TerminatingOrDeleted(body))
            ObserveHiveBody(uid, observer, body);
    }

    private void ObserveHiveBody(EntityUid anchor, ChangelingMindspaceComponent observer, EntityUid body)
    {
        observer.Viewing = body;
        _hiveEye.SetTarget(anchor, body);
        _transform.SetCoordinates(anchor, new EntityCoordinates(body, Vector2.Zero));
        SyncHiveVision(anchor, body);
        Dirty(anchor, observer);
    }

    private void SyncHiveVision(EntityUid anchor, EntityUid body)
    {
        var blocked = _hiveVision.HasFlashImmunityVisionBlockers(body);
        if (!blocked && TryComp<NightVisionComponent>(body, out var night) && night.Active
            && !(night.DisabilityBlockable && HasComp<NightVisionBlockerComponent>(body)))
            EnsureComp<NightVisionComponent>(anchor);
        else
            RemComp<NightVisionComponent>(anchor);
        if (!blocked && TryComp<ThermalVisionComponent>(body, out var thermal) && thermal.Active)
        {
            if (!TryComp<ThermalVisionComponent>(anchor, out var mirrored)
                || mirrored.UseAlternativeShader != thermal.UseAlternativeShader)
            {
                RemComp<ThermalVisionComponent>(anchor);
                AddComp(anchor, new ThermalVisionComponent { Active = true, UseAlternativeShader = thermal.UseAlternativeShader, GrantToggleAction = false });
            }
        }
        else
            RemComp<ThermalVisionComponent>(anchor);
    }

    [SubscribeLocalEvent]
    private void OnLeaveHiveBody(EntityUid uid, ChangelingColonyMemberComponent member, ChangelingLeaveBodyMessage args)
    {
        if (args.Actor == uid && TryComp<ChangelingColonyComponent>(member.Mind, out var colony)
            && _mind.TryGetMind(uid, out var mind, out _) && colony.Minds.ContainsKey(mind))
            ReturnToHive(mind, colony);
    }

    [SubscribeLocalEvent]
    private void OnRecallHiveMind(EntityUid uid, ChangelingColonyMemberComponent member, ChangelingRecallMessage args)
    {
        if (args.Actor != uid || !IsHiveMaster(uid, member)
            || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony))
            return;
        var target = GetEntity(args.Carrier);
        if (colony.Members.Contains(target) && _mind.TryGetMind(target, out var mind, out _)
            && mind != member.Mind && colony.Minds.ContainsKey(mind))
            ReturnToHive(mind, colony);
    }

    private void UpdateHiveObserver(EntityUid anchor, ChangelingMindspaceComponent observer, ChangelingColonyComponent colony)
    {
        var bodies = colony.Members.Where(e => !TerminatingOrDeleted(e)).ToList();
        if (!observer.Bodies.SequenceEqual(bodies))
        {
            if (TryComp<ActorComponent>(anchor, out var previousActor))
            {
                foreach (var removed in observer.Bodies.Except(bodies))
                {
                    if (!Deleted(removed))
                        _hiveViews.RemoveViewSubscriber(removed, previousActor.PlayerSession);
                }
            }
            observer.Bodies = bodies;
            Dirty(anchor, observer);
        }
        if (!TryComp<ActorComponent>(anchor, out var actor))
            return;
        EnsureComp<BlindableComponent>(anchor);
        _hiveBlindness.UpdateIsBlind((anchor, null), bypass: true);
        foreach (var body in bodies)
            _hiveViews.AddViewSubscriber(body, actor.PlayerSession);
        if (observer.Viewing is not { } viewing || !bodies.Contains(viewing))
        {
            if (bodies.Count > 0)
                ObserveHiveBody(anchor, observer, bodies[0]);
        }
        else
            SyncHiveVision(anchor, viewing);
    }

    [SubscribeLocalEvent]
    private void RemoveHiveViews(EntityUid anchor, ChangelingMindspaceComponent observer, ref PlayerDetachedEvent args)
    {
        foreach (var body in observer.Bodies)
        {
            if (!Deleted(body))
                _hiveViews.RemoveViewSubscriber(body, args.Player);
        }
    }

    private void ReleaseHive(ChangelingColonyComponent colony, EntityUid? preservedMind = null)
    {
        var master = !TerminatingOrDeleted(colony.Core)
            ? Comp<ChangelingMindspaceComponent>(colony.Core).Colony : (EntityUid?) null;
        colony.PendingMinds.Clear();
        foreach (var (mind, anchor) in colony.Minds.ToArray())
        {
            CloseHiveConsent(mind);
            if (mind != preservedMind && TryComp<MindComponent>(mind, out var comp) && comp.OwnedEntity != null)
                _mind.TransferTo(mind, null);
            if (!TerminatingOrDeleted(anchor))
                QueueDel(anchor);
        }
        colony.Minds.Clear();
        foreach (var anchor in colony.VacantAnchors)
        {
            if (!TerminatingOrDeleted(anchor))
                QueueDel(anchor);
        }
        colony.VacantAnchors.Clear();
        foreach (var identity in colony.Identities)
        {
            if (!TerminatingOrDeleted(identity))
                QueueDel(identity);
        }
        colony.Identities.Clear();
        foreach (var body in colony.Members)
        {
            if (TerminatingOrDeleted(body))
                continue;
            RemComp<ChangelingComponent>(body);
            RemComp<StoreComponent>(body);
            RemComp<ChangelingColonyMemberComponent>(body);
        }
        colony.Members.Clear();
        if (master is { } owner)
            RemCompDeferred<ChangelingColonyComponent>(owner);
    }

}

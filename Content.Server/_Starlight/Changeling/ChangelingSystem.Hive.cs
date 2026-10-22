using System.Linq;
using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared._Starlight.Changeling;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Changeling.Components;
using Content.Shared.Mind;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Store.Components;
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

    public void EnsureHive(EntityUid body)
    {
        if (HasComp<ChangelingColonyMemberComponent>(body)
            || !_mind.TryGetMind(body, out var mind, out _) || !TryComp<ChangelingComponent>(body, out var ling))
            return;
        var colony = EnsureComp<ChangelingColonyComponent>(mind);
        colony.Active = body;
        colony.Core = CreateMindspace(mind, mind, body, colony);
        var core = EnsureComp<ChangelingComponent>(colony.Core);
        core.Resources = ling.Resources;
        core.ChemicalUpdateCooldown = ling.ChemicalUpdateCooldown;
        core.ChemicalNextUpdateTime = ling.ChemicalNextUpdateTime;
        core.BiomassUpdateCooldown = ling.BiomassUpdateCooldown;
        core.BiomassNextUpdateTime = ling.BiomassNextUpdateTime;
        core.BiomassDrain = ling.BiomassDrain;
        if (TryComp<ChangelingIdentityComponent>(body, out var identity))
            colony.Identities = identity.ConsumedIdentities;
        ShareHiveStore(body, colony.Core);
        EnsureComp<StoreComponent>(colony.Core).AccountOwner = mind;
        AddColonyMember(body, mind, colony);
        EnsureHiveAction(mind, "ActionChangelingColony");
        if (ling.HasChemicalSting)
            CopyStingChemistry(body, colony.Core, ling, core);
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
            new InterfaceData("ChangelingColonyBoundUserInterface", requireInputValidation: false));
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
        var ling = EnsureComp<ChangelingComponent>(body);
        ling.Resources = Comp<ChangelingComponent>(colony.Core).Resources;
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
        Dirty(body, ling);
        Dirty(body, identity);
    }

    private void AbsorbHiveMind(EntityUid eater, EntityUid victim)
    {
        EnsureHive(eater);
        if (!TryGetColony(eater, out var member, out var colony)
            || !_mind.TryGetMind(victim, out var mind, out var victimMind)
            || mind == member.Mind || colony.Minds.ContainsKey(mind))
            return;
        // Existing colonies keep their own membership; absorbing their master must not orphan their players.
        if (TryComp<ChangelingColonyComponent>(mind, out var previous))
            ReleaseHive(previous, mind);
        if (TryComp<ActionsContainerComponent>(mind, out var oldActions))
        {
            foreach (var action in oldActions.Container.ContainedEntities.ToArray())
                QueueDel(action);
        }
        var anchor = CreateMindspace(mind, member.Mind, eater, colony);
        foreach (var objective in victimMind.Objectives.ToArray())
            _mind.TryRemoveObjective(mind, victimMind, objective);
        _changelingRoles.MindAddRole(mind, "MindRoleChangelingHive", victimMind, silent: true);
        _mind.TransferTo(mind, anchor, createGhost: false);
        SyncHiveActions(member.Mind, mind);
        RefreshFragmentActions(anchor, colony);
        UpdateHiveObserver(anchor, Comp<ChangelingMindspaceComponent>(anchor), colony);
        _popup.PopupEntity(Loc.GetString("changeling-hive-absorbed"), anchor, anchor);
        UpdateColonyUi(anchor, colony);
        _chemistryUi.OpenUi(anchor, ChangelingColonyUiKey.Key, anchor);
    }

    private void EnsureHiveAction(EntityUid mind, string prototype)
    {
        if (TryComp<ActionsContainerComponent>(mind, out var actions)
            && actions.Container.ContainedEntities.Any(a => MetaData(a).EntityPrototype?.ID == prototype))
            return;
        _actionContainers.AddAction(mind, prototype);
    }

    private void SyncHiveActions(EntityUid master, EntityUid mind)
    {
        EnsureHiveAction(mind, "ActionChangelingColony");
        if (mind == master || !TryComp<ActionsContainerComponent>(master, out var actions))
            return;
        foreach (var action in actions.Container.ContainedEntities.ToArray())
        {
            if (!TerminatingOrDeleted(action) && MetaData(action).EntityPrototype is { } prototype)
                EnsureHiveAction(mind, prototype.ID);
        }
    }

    private bool IsHiveMaster(EntityUid body, ChangelingColonyMemberComponent member)
        => _mind.TryGetMind(body, out var mind, out _) && mind == member.Mind;

    private bool IsHivePlayer(EntityUid body, ChangelingColonyComponent colony)
        => _mind.TryGetMind(body, out var mind, out _) && colony.Minds.ContainsKey(mind);

    private void OnHivePurchase(ref StorePurchaseAttemptEvent args)
    {
        if (HasComp<ChangelingMindspaceComponent>(args.Buyer)
            || TryGetColony(args.Buyer, out var member, out _) && !IsHiveMaster(args.Buyer, member))
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
        _mind.TransferTo(mind, anchor, createGhost: false);
        if (body is { } vacant && colony.Members.Contains(vacant) && !TerminatingOrDeleted(vacant))
            ActivateVacantCarrier(vacant);
        var observer = Comp<ChangelingMindspaceComponent>(anchor);
        if (body is { } view && colony.Members.Contains(view) && !TerminatingOrDeleted(view))
            ObserveHiveBody(anchor, observer, view);
        UpdateHiveObserver(anchor, observer, colony);
        RefreshFragmentActions(anchor, colony);
        UpdateColonyUi(anchor, colony);
        _chemistryUi.OpenUi(anchor, ChangelingColonyUiKey.Key, anchor);
        return true;
    }

    private void ActivateVacantCarrier(EntityUid body)
    {
        if (!HasComp<HTNComponent>(body))
            AddComp(body, new HTNComponent { RootTask = new HTNCompoundTask { Task = "SimpleHostileCompound" } });
        _factionSystem.ClearFactions(body);
        _factionSystem.AddFaction(body, ColonyFaction);
    }

    private void StopHiveBody(EntityUid body)
    {
        _chemistryUi.CloseUi(body, ChangelingColonyUiKey.Key);
        _chemistryUi.CloseUi(body, ChangelingChemistryUiKey.Key);
        if (!TryComp<ChangelingComponent>(body, out var ling))
            return;
        if (ling.StrainedMusclesActive)
            ToggleStrainedMuscles(body, ling);
        if (ling.StealthEnabled)
            ToggleChameleonSkin(body, ling, false);
        StopAdaptations(body, ling);
        RemoveAllChangelingEquipment(body, ling);
        ling.Equipment.Clear();
    }

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
                AddComp(anchor, new ThermalVisionComponent { Active = true, UseAlternativeShader = thermal.UseAlternativeShader });
            }
        }
        else
            RemComp<ThermalVisionComponent>(anchor);
    }

    private void OnLeaveHiveBody(EntityUid uid, ChangelingColonyMemberComponent member, ChangelingLeaveBodyMessage args)
    {
        if (args.Actor == uid && TryComp<ChangelingColonyComponent>(member.Mind, out var colony)
            && _mind.TryGetMind(uid, out var mind, out _) && colony.Minds.ContainsKey(mind))
            ReturnToHive(mind, colony);
    }

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
        foreach (var (mind, anchor) in colony.Minds.ToArray())
        {
            if (mind != preservedMind && TryComp<MindComponent>(mind, out var comp) && comp.OwnedEntity != null)
                _mind.TransferTo(mind, null);
            if (!TerminatingOrDeleted(anchor))
                QueueDel(anchor);
        }
        colony.Minds.Clear();
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

    private EntityUid StingContainer(EntityUid body)
        => TryGetColony(body, out _, out var colony) && colony.Core.IsValid() ? colony.Core : body;
}

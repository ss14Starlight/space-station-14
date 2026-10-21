using System.Linq;
using Content.Server._Starlight.Medical.Limbs;
using Content.Server.NPC.HTN;
using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Events;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Changeling;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.Physics;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [Dependency] private SharedContainerSystem _colonyContainers = default!;
    [Dependency] private SharedPhysicsSystem _colonyPhysics = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private ChangelingDevourSystem _devour = default!;
    [Dependency] private LimbSystem _colonyLimbs = default!;

    private const int CarrierLimit = 8;
    private const int BloodLimit = 10;
    private const string ColonyFaction = "AllHostile";
    private static readonly EntProtoId NeurotoxinAction = "ActionChangelingNeurotoxin";
    private readonly List<(EntityUid Mind, EntityUid Part)> _detachedParts = new();

    private void InitializeColony()
    {
        SubscribeLocalEvent<ChangelingComponent, LearnChangelingColonyEvent>(OnLearnColony);
        SubscribeLocalEvent<ChangelingComponent, EvolveChangelingEvent>(OnEvolveColony);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, OpenChangelingColonyEvent>(OnOpenColony);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, RuptureChangelingEvent>(OnRupture);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, ChangelingDetachMessage>(OnDetachMessage);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, ChangelingSwitchMessage>(OnSwitchMessage);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, ChangelingAnimateBloodMessage>(OnAnimateMessage);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, BodyPartRemovedEvent>(OnColonyPartRemoved);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, BleedModifierEvent>(OnColonyBleed);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, MobStateChangedEvent>(OnCarrierState);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, EntityTerminatingEvent>(OnCarrierTerminating);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, BeforeChangelingTransformEvent>(OnColonyBeforeTransform);
        SubscribeLocalEvent<ChangelingColonyMemberComponent, AfterChangelingTransformEvent>(OnColonyAfterTransform);
        SubscribeLocalEvent<ActionComponent, ActionAttemptEvent>(OnColonyActionAttempt);
        InitializeColonyCapture();
    }

    private void OnLearnColony(EntityUid uid, ChangelingComponent comp, LearnChangelingColonyEvent args)
    {
        if (!_mind.TryGetMind(uid, out var mind, out _))
            return;
        var colony = EnsureComp<ChangelingColonyComponent>(mind);
        if (colony.Learned)
            return;
        colony.Learned = true;
        colony.Active = uid;
        AddColonyMember(uid, mind, colony);
        _actionContainers.AddAction(mind, "ActionChangelingColony");
        _actionContainers.AddAction(mind, "ActionChangelingRupture");
    }

    private void OnEvolveColony(EntityUid uid, ChangelingComponent comp, EvolveChangelingEvent args)
    {
        if (!_mind.TryGetMind(uid, out var mind, out _))
            return;
        // The flag belongs to the mind and can also be used by other changeling abilities.
        var colony = EnsureComp<ChangelingColonyComponent>(mind);
        colony.Evolved = true;
        RefreshFragmentActions(uid, colony);
    }

    private void AddColonyMember(EntityUid uid, EntityUid mind, ChangelingColonyComponent colony)
    {
        var member = EnsureComp<ChangelingColonyMemberComponent>(uid);
        member.Mind = mind;
        Dirty(uid, member);
        colony.Members.Add(uid);
        EnsureComp<UserInterfaceComponent>(uid);
        _chemistryUi.SetUi(uid, ChangelingColonyUiKey.Key, new InterfaceData("ChangelingColonyBoundUserInterface", requireInputValidation: false));
        RefreshColonyFriends(uid);
    }

    private bool TryGetColony(EntityUid uid, out ChangelingColonyMemberComponent member, out ChangelingColonyComponent colony)
    {
        colony = default!;
        return TryComp(uid, out member!) && TryComp(member.Mind, out colony!);
    }

    private void OnOpenColony(EntityUid uid, ChangelingColonyMemberComponent member, ref OpenChangelingColonyEvent args)
    {
        if (args.Handled || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || colony.Active != uid)
            return;
        args.Handled = true;
        UpdateColonyUi(uid, colony);
        _chemistryUi.TryToggleUi(uid, ChangelingColonyUiKey.Key, uid);
    }

    private void UpdateColonyUi(EntityUid uid, ChangelingColonyComponent colony)
    {
        var parts = new List<ChangelingColonyEntry>();
        if (!Comp<ChangelingColonyMemberComponent>(uid).IsFragment && _mobState.IsAlive(uid))
        {
            foreach (var (part, data) in _body.GetBodyChildren(uid))
            {
                if (data.PartType is BodyPartType.Torso or BodyPartType.Other)
                    continue;
                parts.Add(new(GetNetEntity(part), Name(part)));
            }
        }
        var carriers = colony.Members
            .Where(carrier => carrier != uid && IsLivingCarrier(carrier))
            .Select(carrier => new ChangelingColonyEntry(GetNetEntity(carrier), Name(carrier))).ToList();
        _chemistryUi.SetUiState(uid, ChangelingColonyUiKey.Key, new ChangelingColonyUiState(parts, carriers, colony.AnimateBlood));
    }

    private void OnAnimateMessage(EntityUid uid, ChangelingColonyMemberComponent member, ChangelingAnimateBloodMessage args)
    {
        if (args.Actor != uid || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || colony.Active != uid)
            return;
        colony.AnimateBlood = args.Enabled;
        UpdateColonyUi(uid, colony);
    }

    private void OnSwitchMessage(EntityUid uid, ChangelingColonyMemberComponent member, ChangelingSwitchMessage args)
    {
        if (args.Actor != uid || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || colony.Active != uid)
            return;
        var target = GetEntity(args.Carrier);
        if (colony.Members.Contains(target) && IsLivingCarrier(target))
            TransferColony(member.Mind, colony, target);
    }

    private void OnDetachMessage(EntityUid uid, ChangelingColonyMemberComponent member, ChangelingDetachMessage args)
    {
        if (args.Actor != uid || member.IsFragment || !_mobState.IsAlive(uid)
            || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || colony.Active != uid
            || !TryComp<ChangelingComponent>(uid, out var ling))
            return;
        var part = GetEntity(args.Part);
        if (!TryComp<BodyPartComponent>(part, out var data) || data.Body != uid
            || data.PartType is BodyPartType.Torso or BodyPartType.Other || CountCarriers(colony) >= CarrierLimit)
            return;
        var cost = data.PartType is BodyPartType.Hand or BodyPartType.Foot ? 3 : data.PartType == BodyPartType.Head ? 8 : 5;
        if (ling.Chemicals < 15 || ling.Biomass < cost)
            return;
        colony.SuppressAnimation = true;
        try
        {
            if (!DetachColonyPart(uid, part))
                return;
            UpdateChemicals(uid, ling, -15);
            UpdateBiomass(uid, ling, -cost);
            AnimatePart(part, member.Mind, colony);
            PlayMeatySound(uid, ling);
        }
        finally
        {
            colony.SuppressAnimation = false;
        }
        UpdateColonyUi(uid, colony);
    }

    private bool DetachColonyPart(EntityUid body, EntityUid part)
    {
        if (TryComp<HumanoidAppearanceComponent>(body, out var appearance)
            && TryComp<BodyComponent>(body, out var anatomy) && TryComp<BodyPartComponent>(part, out var limb)
            && limb.PartType != BodyPartType.Torso)
            return _colonyLimbs.Amputatate((body, Transform(body), appearance, anatomy),
                (part, Transform(part), MetaData(part), limb));
        return _colonyContainers.TryGetContainingContainer((part, null, null), out var container)
            && _colonyContainers.Remove(part, container, force: true, destination: Transform(body).Coordinates);
    }

    private void OnRupture(EntityUid uid, ChangelingColonyMemberComponent member, ref RuptureChangelingEvent args)
    {
        if (args.Handled || member.IsFragment || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony)
            || colony.Active != uid || !TryComp<ChangelingComponent>(uid, out var ling) || ling.Biomass < 20
            || _body.GetRootPartOrNull(uid) is not { } root)
            return;
        var pieces = _body.GetBodyChildren(uid)
            .Where(part => part.Id == root.Entity || _body.GetParentPartOrNull(part.Id) == root.Entity)
            .Select(part => part.Id).ToList();
        if (pieces.Count == 0 || CountCarriers(colony) + pieces.Count - (_mobState.IsAlive(uid) ? 1 : 0) > CarrierLimit)
            return;
        args.Handled = true;
        colony.SuppressAnimation = true;
        colony.Members.Remove(uid);
        try
        {
            var animated = new List<EntityUid>();
            // Remove the torso last, so the body still owns its other slots during amputation.
            foreach (var part in pieces.OrderBy(part => part == root.Entity))
            {
                if (DetachColonyPart(uid, part) && AnimatePart(part, member.Mind, colony))
                    animated.Add(part);
            }
            if (animated.Count == 0)
                return;
            UpdateBiomass(uid, ling, -20);
            PlayMeatySound(uid, ling);
            if (TransferColony(member.Mind, colony, animated[0]))
                _gibbing.Gib(uid);
        }
        finally
        {
            colony.SuppressAnimation = false;
            if (colony.Active == uid)
                colony.Members.Add(uid);
        }
    }

    private int CountCarriers(ChangelingColonyComponent colony)
        => colony.Members.Count(IsLivingCarrier);

    private bool IsLivingCarrier(EntityUid uid)
        => !TerminatingOrDeleted(uid) && _mobState.IsAlive(uid)
           && TryComp<ChangelingColonyMemberComponent>(uid, out var member) && !member.IsBlood;

    private bool AnimatePart(EntityUid uid, EntityUid mind, ChangelingColonyComponent colony)
    {
        if (TerminatingOrDeleted(uid) || !TryComp<BodyPartComponent>(uid, out var part) || part.Body != null
            || HasComp<ChangelingColonyMemberComponent>(uid) || CountCarriers(colony) >= CarrierLimit
            || _body.GetParentPartOrNull(uid) != null)
            return false;
        EntProtoId prototype = part.PartType switch
        {
            BodyPartType.Hand or BodyPartType.Foot => "ChangelingFragmentSmall",
            BodyPartType.Head => "ChangelingFragmentHead",
            BodyPartType.Torso => "ChangelingFragmentTorso",
            _ => "ChangelingFragmentLimb",
        };
        // Keep the actual part, its sprite, organs and child parts. Add only missing mob components.
        EntityManager.AddComponents(uid, _proto.Index(prototype), removeExisting: false);
        var member = Comp<ChangelingColonyMemberComponent>(uid);
        member.IsFragment = true;
        _colonyPhysics.SetBodyType(uid, BodyType.KinematicController);
        if (TryComp<FixturesComponent>(uid, out var fixtures))
        {
            var small = part.PartType is BodyPartType.Hand or BodyPartType.Foot;
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                _colonyPhysics.SetCollisionMask(uid, id, fixture, (int) (small ? CollisionGroup.SmallMobMask : CollisionGroup.MobMask));
                _colonyPhysics.SetCollisionLayer(uid, id, fixture, (int) (small ? CollisionGroup.SmallMobLayer : CollisionGroup.MobLayer));
            }
        }
        _damage.SetAllDamage(uid, FixedPoint2.Zero);
        _mobState.ChangeMobState(uid, MobState.Alive);
        AddColonyMember(uid, mind, colony);
        _actions.AddAction(uid, ref member.NeuroAction, NeurotoxinAction);
        return true;
    }

    private void OnColonyPartRemoved(EntityUid uid, ChangelingColonyMemberComponent member, ref BodyPartRemovedEvent args)
    {
        if (!member.IsBlood && TryComp<ChangelingColonyComponent>(member.Mind, out var colony)
            && colony.AnimateBlood && !colony.SuppressAnimation && !colony.Moving)
            _detachedParts.Add((member.Mind, args.Part.Owner));
    }

    private void OnColonyBleed(EntityUid uid, ChangelingColonyMemberComponent member, ref BleedModifierEvent args)
    {
        if (member.IsBlood || args.BleedAmount <= 0 || _timing.CurTime < member.NextBlood
            || !TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || !colony.AnimateBlood
            || colony.SuppressAnimation || !TryComp<BloodstreamComponent>(uid, out var blood)
            || blood.BloodSolution is not { } solution || solution.Comp.Solution.Volume <= 0
            || colony.Members.Count(e => !TerminatingOrDeleted(e) && HasComp<ChangelingColonyMemberComponent>(e)
                && Comp<ChangelingColonyMemberComponent>(e).IsBlood) >= BloodLimit)
            return;
        member.NextBlood = _timing.CurTime + TimeSpan.FromSeconds(3);
        var drop = Spawn("MobChangelingBlood", Transform(uid).Coordinates);
        AddColonyMember(drop, member.Mind, colony);
    }

    private void RefreshColonyFriends(EntityUid member)
    {
        var changelings = EntityQueryEnumerator<ChangelingComponent>();
        while (changelings.MoveNext(out var uid, out _))
            _factionSystem.IgnoreEntity(member, uid);
        var members = EntityQueryEnumerator<ChangelingColonyMemberComponent>();
        while (members.MoveNext(out var uid, out _))
        {
            _factionSystem.IgnoreEntity(member, uid);
            _factionSystem.IgnoreEntity(uid, member);
        }
    }

    private void OnCarrierState(EntityUid uid, ChangelingColonyMemberComponent member, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            EndCapture(uid, member);
            if (member.IsFragment)
                TryFallbackCarrier(uid, member);
        }
    }

    private void OnCarrierTerminating(EntityUid uid, ChangelingColonyMemberComponent member, ref EntityTerminatingEvent args)
    {
        EndCapture(uid, member);
        TryFallbackCarrier(uid, member);
        if (TryComp<ChangelingColonyComponent>(member.Mind, out var colony))
            colony.Members.Remove(uid);
    }

    private void TryFallbackCarrier(EntityUid uid, ChangelingColonyMemberComponent member)
    {
        if (!TryComp<ChangelingColonyComponent>(member.Mind, out var colony) || colony.Moving || colony.Active != uid)
            return;
        foreach (var other in colony.Members.ToArray())
        {
            if (other != uid && IsLivingCarrier(other) && TransferColony(member.Mind, colony, other))
                break;
        }
    }

    private void OnColonyBeforeTransform(EntityUid uid, ChangelingColonyMemberComponent member, BeforeChangelingTransformEvent args)
    {
        if (TryComp<ChangelingColonyComponent>(member.Mind, out var colony))
            colony.SuppressAnimation = true;
    }

    private void OnColonyAfterTransform(EntityUid uid, ChangelingColonyMemberComponent member, AfterChangelingTransformEvent args)
    {
        if (TryComp<ChangelingColonyComponent>(member.Mind, out var colony))
            colony.SuppressAnimation = false;
    }

    private void UpdateColonies()
    {
        foreach (var (mind, part) in _detachedParts.ToArray())
        {
            if (TryComp<ChangelingColonyComponent>(mind, out var colony))
                AnimatePart(part, mind, colony);
        }
        _detachedParts.Clear();
        UpdateColonyCaptures();
        var query = EntityQueryEnumerator<ChangelingColonyComponent>();
        while (query.MoveNext(out _, out var colony))
        {
            if (_timing.CurTime < colony.NextUpdate || TerminatingOrDeleted(colony.Active))
                continue;
            colony.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
            if (_chemistryUi.IsUiOpen(colony.Active, ChangelingColonyUiKey.Key))
                UpdateColonyUi(colony.Active, colony);
            RefreshFragmentActions(colony.Active, colony);
        }
    }

    private bool IsFragmentActionAllowed(EntityUid user, EntityUid action)
    {
        if (!TryGetColony(user, out var member, out var colony) || !member.IsFragment)
            return true;
        var prototype = MetaData(action).EntityPrototype?.ID;
        return prototype is "ActionChangelingColony" or "ActionChangelingPossess" or "ActionChangelingNeurotoxin" or "VentCrawlExitAction"
            || colony.Evolved && member.EvolvedAction is { } evolved && prototype == evolved.Id;
    }

    private void OnColonyActionAttempt(Entity<ActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (!IsFragmentActionAllowed(args.User, ent.Owner))
            args.Cancelled = true;
    }

    private void RefreshFragmentActions(EntityUid uid, ChangelingColonyComponent colony)
    {
        if (!TryComp<ChangelingColonyMemberComponent>(uid, out var member))
            return;
        if (member.IsFragment && colony.Evolved && member.EvolvedAction is { } evolved && member.EvolvedActionEntity == null)
            _actions.AddAction(uid, ref member.EvolvedActionEntity, evolved);
        foreach (var action in _actions.GetActions(uid).ToArray())
        {
            if (!IsFragmentActionAllowed(uid, action.Owner))
            {
                colony.DisabledActions.TryAdd(action.Owner, action.Comp.Enabled);
                _actions.SetEnabled(action.AsNullable(), false);
            }
            else if (colony.DisabledActions.Remove(action.Owner, out var enabled))
                _actions.SetEnabled(action.AsNullable(), enabled);
        }
    }
}

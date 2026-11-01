// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Medical.PartDamage;

public sealed partial class BodyPartDamageSystem : EntitySystem
{
    [Dependency] private SLBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly ProtoId<DamageGroupPrototype>[] LocalGroups = ["Brute", "Burn"];
    public static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly FixedPoint2 MangledIntegrity = 1;

    private readonly HashSet<ProtoId<DamageTypePrototype>> _localTypes = new();
    private readonly Dictionary<EntityUid, BodyDamageTargeting> _targeting = new();
    private readonly Dictionary<EntityUid, (EntityUid Part, GameTick Tick)> _pendingHit = new();
    private readonly HashSet<EntityUid> _syncing = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SLBodyComponent, DamageTargetPartEvent>(OnTargetPart);
        SubscribeLocalEvent<SLBodyComponent, DamageChangedEvent>(OnBodyDamageChanged);
        SubscribeLocalEvent<SLBodyComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<BodyPartDamageComponent, BodyPartEnteredBodyEvent>(OnPartEntered);
        SubscribeLocalEvent<BodyPartDamageComponent, BodyPartLeftBodyEvent>(OnPartLeft);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        CacheLocalTypes();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<DamageGroupPrototype>())
            CacheLocalTypes();
    }

    private void CacheLocalTypes()
    {
        _localTypes.Clear();
        foreach (var group in LocalGroups)
        {
            if (_prototypes.TryIndex(group, out var proto))
                _localTypes.UnionWith(proto.DamageTypes);
        }
    }

    public TargetingScope Target(EntityUid body, BodyDamageTargeting targeting)
    {
        var previous = _targeting.TryGetValue(body, out var old) ? old : (BodyDamageTargeting?) null;
        _targeting[body] = targeting;
        return new TargetingScope(this, body, previous);
    }

    public readonly struct TargetingScope(BodyPartDamageSystem system, EntityUid body, BodyDamageTargeting? previous) : IDisposable
    {
        public void Dispose()
        {
            if (previous is { } old)
                system._targeting[body] = old;
            else
                system._targeting.Remove(body);
        }
    }

    private BodyDamageTargeting GetTargeting(EntityUid body)
        => _targeting.TryGetValue(body, out var targeting) ? targeting : BodyDamageTargeting.Default;

    public bool IsLocalType(ProtoId<DamageTypePrototype> type) => _localTypes.Contains(type);

    public List<Entity<BodyPartDamageComponent>> GetDamageableParts(Entity<SLBodyComponent> body)
    {
        var list = new List<Entity<BodyPartDamageComponent>>();
        foreach (var part in _body.GetBodyParts(body))
        {
            if (TryComp<BodyPartDamageComponent>(part, out var damage))
                list.Add((part.Owner, damage));
        }

        return list;
    }

    public FixedPoint2 GetPartDamage(BodyPartDamageComponent part, ProtoId<DamageTypePrototype> type)
        => part.Damage.DamageDict.GetValueOrDefault(type);

    public FixedPoint2 GetPartDamageTotal(BodyPartDamageComponent part)
        => part.Damage.GetTotal();

    public float GetHealthFraction(BodyPartDamageComponent part)
    {
        if (part.MaxHealth <= 0)
            return 0f;
        var damage = FixedPoint2.Min(part.Damage.GetTotal(), part.MaxHealth);
        return ((part.MaxHealth - damage) / part.MaxHealth).Float();
    }

    private void OnRejuvenate(Entity<SLBodyComponent> body, ref RejuvenateEvent args)
    {
        foreach (var part in GetDamageableParts(body))
            SetIntegrity(body, part, part.Comp.MaxIntegrity);
    }

    #region Hit selection

    private void OnTargetPart(Entity<SLBodyComponent> body, ref DamageTargetPartEvent args)
    {
        if (_net.IsClient || _syncing.Contains(body) || !HasLocalDamage(args.Damage, positive: true))
            return;

        var targeting = GetTargeting(body);
        if (targeting.Mode == BodyDamageTargetingMode.Spread)
            return; // every part, so every slot

        if (PickHitPart(body, targeting, args.Origin) is not { } part)
            return;

        _pendingHit[body] = (part.Owner, _timing.CurTick);
        args.ArmorSlots = part.Comp.ArmorSlots;
    }

    private Entity<BodyPartDamageComponent>? PickHitPart(Entity<SLBodyComponent> body, BodyDamageTargeting targeting, EntityUid? origin)
    {
        var parts = GetDamageableParts(body);
        if (parts.Count == 0)
            return null;

        switch (targeting.Mode)
        {
            case BodyDamageTargetingMode.Part:
                foreach (var part in parts)
                {
                    if (part.Owner == targeting.Part)
                        return part;
                }
                break;
            case BodyDamageTargetingMode.Legs:
                var legs = parts.Where(p => Comp<SLBodyPartComponent>(p).Kind is BodyPartType.Leg or BodyPartType.Foot).ToList();
                if (legs.Count > 0)
                    parts = legs;
                break;
        }

        var weights = GetHitWeights(body, parts, origin);
        var total = weights.Sum(w => w.Weight);
        if (total <= 0f)
            return _random.Pick(parts);

        var roll = _random.NextFloat(total);
        foreach (var (uid, weight) in weights)
        {
            roll -= weight;
            if (roll < 0f)
                return (uid, Comp<BodyPartDamageComponent>(uid));
        }

        return parts[^1];
    }

    private List<(EntityUid Part, float Weight)> GetHitWeights(Entity<SLBodyComponent> body, List<Entity<BodyPartDamageComponent>> parts, EntityUid? origin)
    {
        var weights = new List<(EntityUid Part, float Weight)>(parts.Count);
        foreach (var part in parts)
            weights.Add((part.Owner, MathF.Max(0f, part.Comp.HitWeight)));

        var ev = new BodyPartHitWeightsEvent(weights, origin);
        RaiseLocalEvent(body, ref ev);
        return weights;
    }

    #endregion

    #region Reconciliation

    private void OnBodyDamageChanged(Entity<SLBodyComponent> body, ref DamageChangedEvent args)
    {
        if (_net.IsClient || _syncing.Contains(body) || _timing.ApplyingState)
            return;

        EntityUid? hitPart = null;
        if (_pendingHit.Remove(body, out var pending) && pending.Tick == _timing.CurTick)
            hitPart = pending.Part;

        Reconcile(body, args.Damageable, hitPart, args.Origin);
    }

    private void Reconcile(Entity<SLBodyComponent> body, DamageableComponent damageable, EntityUid? hitPart, EntityUid? origin)
    {
        var parts = GetDamageableParts(body);
        if (parts.Count == 0)
            return;

        var targeting = GetTargeting(body);
        var bodyDamage = _damageable.GetAllDamage((body.Owner, damageable));
        foreach (var type in _localTypes)
        {
            var bodyValue = bodyDamage.DamageDict.GetValueOrDefault(type);
            var partsValue = FixedPoint2.Zero;
            foreach (var part in parts)
                partsValue += part.Comp.Damage.DamageDict.GetValueOrDefault(type);

            var diff = bodyValue - partsValue;
            if (diff > 0)
                AddDamage(body, parts, type, diff, targeting, hitPart, origin);
            else if (diff < 0)
                RemoveDamage(parts, type, -diff, targeting);
        }

        foreach (var part in parts)
            Dirty(part);
    }

    private void AddDamage(
        Entity<SLBodyComponent> body,
        List<Entity<BodyPartDamageComponent>> parts,
        ProtoId<DamageTypePrototype> type,
        FixedPoint2 amount,
        BodyDamageTargeting targeting,
        EntityUid? hitPart,
        EntityUid? origin)
    {
        if (targeting.Mode == BodyDamageTargetingMode.Spread)
        {
            var weights = GetHitWeights(body, parts, origin);
            var total = weights.Sum(w => w.Weight);
            var left = amount;
            for (var i = 0; i < parts.Count; i++)
            {
                var share = i == parts.Count - 1 || total <= 0f
                    ? left
                    : FixedPoint2.Min(left, amount * (weights[i].Weight / total));
                if (share <= 0)
                    continue;
                ApplyToPart(body, parts[i], type, share);
                left -= share;
            }
            return;
        }

        Entity<BodyPartDamageComponent>? target = null;
        foreach (var part in parts)
        {
            if (part.Owner == hitPart)
                target = part;
        }
        target ??= PickHitPart(body, targeting, origin);

        if (target != null)
            ApplyToPart(body, target.Value, type, amount);
    }

    private void ApplyToPart(EntityUid body, Entity<BodyPartDamageComponent> part, ProtoId<DamageTypePrototype> type, FixedPoint2 amount)
    {
        var before = part.Comp.Damage.GetTotal();
        var room = FixedPoint2.Max(FixedPoint2.Zero, part.Comp.MaxHealth - before);
        var toHealth = FixedPoint2.Min(amount, room);
        var overflow = amount - toHealth;

        var share = part.Comp.Penetration.TryGetValue(type, out var penetration) ? penetration : part.Comp.DefaultPenetration;
        part.Comp.Damage.DamageDict[type] = part.Comp.Damage.DamageDict.GetValueOrDefault(type) + amount;

        // Damage past the part's health is structural; while it still has health, only a share is.
        var structural = toHealth * share + overflow;
        var integrity = part.Comp.Integrity - structural;

        // Only a hard enough hit of the right kind breaks a part; wearing it down just leaves it hanging on.
        if (integrity <= 0 && (structural < part.Comp.MinDestroyingHit || !part.Comp.DestroyedBy.Contains(type)))
            integrity = FixedPoint2.Min(part.Comp.Integrity, MangledIntegrity);

        // Enough burns char the part away, however slowly they came.
        if (type == HeatType && IsBurnedAway(body, part))
            integrity = FixedPoint2.Zero;

        SetIntegrity(body, part, integrity, FixedPoint2.Max(FixedPoint2.Zero, -integrity), type);
    }

    private bool IsBurnedAway(EntityUid body, Entity<BodyPartDamageComponent> part)
    {
        var threshold = part.Comp.BurnThreshold ?? part.Comp.MaxHealth + part.Comp.MaxIntegrity;
        if (part.Comp.Damage.DamageDict.GetValueOrDefault(HeatType) < threshold)
            return false;

        return !TryComp<SLBodyPartComponent>(part, out var slPart)
            || slPart.Kind != BodyPartType.Torso
            || _mobState.IsDead(body);
    }

    public void SetIntegrity(
        EntityUid body,
        Entity<BodyPartDamageComponent> part,
        FixedPoint2 integrity,
        FixedPoint2 overkill = default,
        ProtoId<DamageTypePrototype>? cause = null)
    {
        var old = part.Comp.Integrity;
        integrity = FixedPoint2.Clamp(integrity, FixedPoint2.Zero, part.Comp.MaxIntegrity);
        if (integrity == old)
            return;

        part.Comp.Integrity = integrity;
        Dirty(part);

        var ev = new BodyPartIntegrityChangedEvent(body, part, old, integrity, overkill, cause);
        RaiseLocalEvent(part, ref ev);
        RaiseLocalEvent(body, ref ev);
    }

    private void RemoveDamage(List<Entity<BodyPartDamageComponent>> parts, ProtoId<DamageTypePrototype> type, FixedPoint2 amount, BodyDamageTargeting targeting)
    {
        var damaged = parts.Where(p => p.Comp.Damage.DamageDict.GetValueOrDefault(type) > 0).ToList();
        if (damaged.Count == 0)
            return;

        switch (targeting.Mode)
        {
            case BodyDamageTargetingMode.MostDamaged:
                damaged.Sort((a, b) => b.Comp.Damage.GetTotal().CompareTo(a.Comp.Damage.GetTotal()));
                break;
            case BodyDamageTargetingMode.Random:
                _random.Shuffle(damaged);
                break;
            case BodyDamageTargetingMode.Part:
                damaged.Sort((a, b) => (b.Owner == targeting.Part).CompareTo(a.Owner == targeting.Part));
                break;
            default:
                RemoveProportionally(damaged, type, amount);
                return;
        }

        var left = amount;
        foreach (var part in damaged)
        {
            if (left <= 0)
                break;
            left -= TakeDamage(part, type, left);
        }
    }

    private void RemoveProportionally(List<Entity<BodyPartDamageComponent>> damaged, ProtoId<DamageTypePrototype> type, FixedPoint2 amount)
    {
        var total = FixedPoint2.Zero;
        foreach (var part in damaged)
            total += part.Comp.Damage.DamageDict[type];

        var left = amount;
        for (var i = 0; i < damaged.Count && left > 0; i++)
        {
            var value = damaged[i].Comp.Damage.DamageDict[type];
            var share = i == damaged.Count - 1 ? left : FixedPoint2.Min(left, amount * (value / total));
            left -= TakeDamage(damaged[i], type, share);
        }

        // Rounding can leave a sliver on a part that ran out earlier.
        foreach (var part in damaged)
        {
            if (left <= 0)
                break;
            left -= TakeDamage(part, type, left);
        }
    }

    private static FixedPoint2 TakeDamage(Entity<BodyPartDamageComponent> part, ProtoId<DamageTypePrototype> type, FixedPoint2 amount)
    {
        var dict = part.Comp.Damage.DamageDict;
        var value = dict.GetValueOrDefault(type);
        var taken = FixedPoint2.Min(value, amount);
        if (value - taken <= 0)
            dict.Remove(type);
        else
            dict[type] = value - taken;
        return taken;
    }

    #endregion

    #region Attaching and detaching

    public void MoveDamageToParent(Entity<SLBodyPartComponent> part)
    {
        if (part.Comp.Parent is not { } parentUid || !TryComp<BodyPartDamageComponent>(parentUid, out var parent))
            return;

        foreach (var child in _body.GetSubtree(part))
        {
            if (!TryComp<BodyPartDamageComponent>(child, out var damage) || damage.Damage.Empty)
                continue;

            foreach (var (type, value) in damage.Damage.DamageDict)
                parent.Damage.DamageDict[type] = parent.Damage.DamageDict.GetValueOrDefault(type) + value;
            damage.Damage.DamageDict.Clear();
            Dirty(child.Owner, damage);
        }

        Dirty(parentUid, parent);
    }

    private void OnPartEntered(Entity<BodyPartDamageComponent> part, ref BodyPartEnteredBodyEvent args)
        => SyncBodyDamage(args.Body, part.Comp.Damage);

    private void OnPartLeft(Entity<BodyPartDamageComponent> part, ref BodyPartLeftBodyEvent args)
    {
        if (TerminatingOrDeleted(args.Body))
            return;
        SyncBodyDamage(args.Body, -part.Comp.Damage);
    }

    private void SyncBodyDamage(EntityUid body, DamageSpecifier delta)
    {
        if (_net.IsClient || _timing.ApplyingState || delta.Empty || delta.GetTotal() == 0)
            return;

        _syncing.Add(body);
        try
        {
            _damageable.ChangeDamage(body, delta, ignoreResistances: true, interruptsDoAfters: false, ignoreGlobalModifiers: true);
        }
        finally
        {
            _syncing.Remove(body);
        }
    }

    #endregion

    private bool HasLocalDamage(DamageSpecifier damage, bool positive)
    {
        foreach (var (type, value) in damage.DamageDict)
        {
            if (_localTypes.Contains(type) && (positive ? value > 0 : value < 0))
                return true;
        }

        return damage.DamageGroupDict.Count > 0;
    }
}

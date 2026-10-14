using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Body.Systems;

public sealed partial class OrganFailureSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> _bloodlossDamageTypeId = "Bloodloss";
    private static readonly ProtoId<DamageTypePrototype> _poisonDamageTypeId = "Poison";
    private static readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1);
    private const string HeartOrganSlot = "heart";
    private const string LiverOrganSlot = "liver";
    private const string KidneysOrganSlot = "kidneys";
    private const float DamagePerMissingHeart = 1f;
    private const float DamagePerMissingChemOrgan = 0.1f;

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private DamageTypePrototype _bloodlossDamage = default!;
    private DamageTypePrototype _poisonDamage = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        _bloodlossDamage = _prototypes.Index(_bloodlossDamageTypeId);
        _poisonDamage = _prototypes.Index(_poisonDamageTypeId);
        _nextUpdate = _timing.CurTime + _updateInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _timing.CurTime;
        if (currentTime < _nextUpdate)
            return;

        _nextUpdate = currentTime + _updateInterval;

        var query = EntityQueryEnumerator<BodyComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var body, out _))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (body.Prototype is not { } prototypeId
                || !_prototypes.TryIndex(prototypeId, out var prototype))
                continue;

            var missingChemOrgan = 0;
            var missingHeart = HasDefaultOrgan(uid, body, prototype, HeartOrganSlot)
                && !_body.TryGetOrgansWithComponent<OrganHeartComponent>(uid, out _);

            if (HasDefaultOrgan(uid, body, prototype, LiverOrganSlot)
                && !_body.TryGetOrgansWithComponent<OrganLiverComponent>(uid, out _))
                missingChemOrgan++;

            if (HasDefaultOrgan(uid, body, prototype, KidneysOrganSlot)
                && !_body.TryGetOrgansWithComponent<OrganKidneysComponent>(uid, out _))
                missingChemOrgan++;

            if (missingChemOrgan > 0)
            {
                var poisonDamage = new DamageSpecifier(
                    _poisonDamage,
                    FixedPoint2.New(DamagePerMissingChemOrgan * missingChemOrgan));
                _damageable.TryChangeDamage(uid, poisonDamage, interruptsDoAfters: false);
            }

            if (missingHeart)
            {
                var bloodlossDamage = new DamageSpecifier(
                    _bloodlossDamage,
                    FixedPoint2.New(DamagePerMissingHeart));
                _damageable.TryChangeDamage(uid, bloodlossDamage, interruptsDoAfters: false);
            }
        }
    }

    private bool HasDefaultOrgan(EntityUid bodyId, BodyComponent body, BodyPrototype prototype, string organSlot)
    {
        if (!_body.HasOrganSlot(bodyId, body, organSlot))
            return false;

        foreach (var slot in prototype.Slots.Values)
        {
            if (slot.Organs.TryGetValue(organSlot, out var organPrototype)
                && organPrototype != "null")
                return true;
        }

        return false;
    }
}

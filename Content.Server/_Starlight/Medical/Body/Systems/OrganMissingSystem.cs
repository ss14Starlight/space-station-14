using Content.Shared._Starlight.Medical.Body.Components;
using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Body.Systems;

public sealed partial class OrganMissingSystem : EntitySystem
{
    private static readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1);

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Dictionary<string, List<OrganMissingRule>> _organMissingByBodyPrototype = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        CollectOrganMissingConfigurations();
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
            if (_mobState.IsDead(uid)
                || body.Prototype is not { } prototypeId
                || !_prototypes.TryIndex(prototypeId, out var prototype)
                || !_organMissingByBodyPrototype.TryGetValue(prototype.ID, out var missingOrgans))
                continue;

            DamageSpecifier? damage = null;
            foreach (var missingOrgan in missingOrgans)
            {
                if (!IsOrganMissing(uid, body, prototype, missingOrgan))
                    continue;

                var organDamage = missingOrgan.DamagePerSecond * (float)_updateInterval.TotalSeconds;
                damage = damage is null ? organDamage : damage + organDamage;
            }

            if (damage is not null)
                _damageable.TryChangeDamage(uid, damage, interruptsDoAfters: false);
        }
    }

    private void CollectOrganMissingConfigurations()
    {
        foreach (var bodyPrototype in _prototypes.EnumeratePrototypes<BodyPrototype>())
        {
            var missingOrgans = new List<OrganMissingRule>();
            foreach (var (bodyPartSlot, slot) in bodyPrototype.Slots)
            {
                foreach (var (organSlot, organPrototypeId) in slot.Organs)
                {
                    if (organPrototypeId == "null"
                        || !_prototypes.TryIndex(organPrototypeId, out EntityPrototype? organPrototype)
                        || !organPrototype.TryComp(out OrganMissingComponent? organMissing, _componentFactory))
                        continue;

                    missingOrgans.Add(new OrganMissingRule(
                        bodyPartSlot,
                        organSlot,
                        new DamageSpecifier(organMissing.DamagePerSecond)));
                }
            }

            if (missingOrgans.Count > 0)
                _organMissingByBodyPrototype.Add(bodyPrototype.ID, missingOrgans);
        }
    }

    private bool IsOrganMissing(
        EntityUid bodyId,
        BodyComponent body,
        BodyPrototype bodyPrototype,
        OrganMissingRule missingOrgan)
    {
        var part = GetBodyPartForSlot(bodyId, body, bodyPrototype, missingOrgan.BodyPartSlot);
        if (part is not { } bodyPart
            || !_body.CanInsertOrgan(bodyPart.Id, missingOrgan.OrganSlot, bodyPart.Component))
            return false;

        var containerId = SharedBodySystem.GetOrganContainerId(missingOrgan.OrganSlot);
        return !_containers.TryGetContainer(bodyPart.Id, containerId, out var container)
            || container.ContainedEntities.Count == 0
            || !HasComp<OrganComponent>(container.ContainedEntities[0]);
    }

    private (EntityUid Id, BodyPartComponent Component)? GetBodyPartForSlot(
        EntityUid bodyId,
        BodyComponent body,
        BodyPrototype bodyPrototype,
        string bodyPartSlot)
    {
        if (bodyPartSlot == bodyPrototype.Root)
        {
            if (body.RootContainer.ContainedEntities.Count == 0)
                return null;

            var rootPartId = body.RootContainer.ContainedEntities[0];
            return TryComp<BodyPartComponent>(rootPartId, out var rootPart)
                ? (rootPartId, rootPart)
                : null;
        }

        foreach (var part in _body.GetBodyChildren(bodyId, body))
        {
            var parentPartAndSlot = _body.GetParentPartAndSlotOrNull(part.Id);
            if (parentPartAndSlot is { } parent && parent.Slot == bodyPartSlot)
                return part;
        }

        return null;
    }

    private readonly record struct OrganMissingRule(
        string BodyPartSlot,
        string OrganSlot,
        DamageSpecifier DamagePerSecond);
}

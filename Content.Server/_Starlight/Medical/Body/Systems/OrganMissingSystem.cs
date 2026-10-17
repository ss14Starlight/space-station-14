using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Medical.Body.Systems;

/// <summary>
/// Bodies hurt while an organ they were built with, and that has <see cref="OrganMissingComponent"/>, is missing
/// from its socket (or something else sits where it goes). A part that is missing altogether does not count.
/// </summary>
public sealed partial class OrganMissingSystem : EntitySystem
{
    private static readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1);

    [Dependency] private SLBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly Dictionary<ProtoId<BodyPrefabPrototype>, List<OrganMissingRule>> _organMissingByPrefab = new();
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

        var query = EntityQueryEnumerator<SLBodyComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var body, out _))
        {
            if (_mobState.IsDead(uid)
                || !_organMissingByPrefab.TryGetValue(body.Prefab, out var missingOrgans))
                continue;

            DamageSpecifier? damage = null;
            foreach (var missingOrgan in missingOrgans)
            {
                if (!IsOrganMissing(body, missingOrgan))
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
        foreach (var prefab in _prototypes.EnumeratePrototypes<BodyPrefabPrototype>())
        {
            var missingOrgans = new List<OrganMissingRule>();
            CollectRules(prefab.Root, [], missingOrgans);
            if (missingOrgans.Count > 0)
                _organMissingByPrefab.Add(prefab.ID, missingOrgans);
        }
    }

    private void CollectRules(BodyPartDef def, List<string> path, List<OrganMissingRule> rules)
    {
        foreach (var children in new[] { def.ContainedParts, def.AttachedParts })
        {
            if (children == null)
                continue;

            foreach (var (socketId, childDef) in children)
            {
                if (_prototypes.TryIndex(childDef.BodyPart, out var childPrototype)
                    && childPrototype.TryComp(out OrganMissingComponent? organMissing, _componentFactory))
                {
                    rules.Add(new OrganMissingRule(path.ToArray(), socketId, new DamageSpecifier(organMissing.DamagePerSecond)));
                }

                CollectRules(childDef, [.. path, socketId], rules);
            }
        }
    }

    private bool IsOrganMissing(SLBodyComponent body, OrganMissingRule missingOrgan)
    {
        if (GetPart(body, missingOrgan.PartPath) is not { } part
            || !part.Comp.Sockets.ContainsKey(missingOrgan.Socket))
            return false;

        // Something that is not an organ, put where the organ goes.
        if (_containers.TryGetContainer(part, SharedBodySystem.GetOrganContainerId(SharedBodySystem.GetLegacySlotName(missingOrgan.Socket)), out var items)
            && items.ContainedEntities.Count > 0)
            return true;

        return !_body.TryGetPartInSocket(part, missingOrgan.Socket, out var organ) || !HasComp<OrganComponent>(organ);
    }

    /// <summary>
    /// The part at the socket path from the root part, if the body still has it.
    /// </summary>
    private Entity<SLBodyPartComponent>? GetPart(SLBodyComponent body, string[] path)
    {
        if (body.RootPartEntity is not { } rootUid || !TryComp<SLBodyPartComponent>(rootUid, out var root))
            return null;

        Entity<SLBodyPartComponent> part = (rootUid, root);
        foreach (var socket in path)
        {
            if (!_body.TryGetPartInSocket(part, socket, out var child))
                return null;
            part = child;
        }

        return part;
    }

    private readonly record struct OrganMissingRule(
        string[] PartPath,
        string Socket,
        DamageSpecifier DamagePerSecond);
}

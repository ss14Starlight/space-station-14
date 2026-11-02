using Content.Shared.Mech.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.Turrets;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.NPC.Systems;

public sealed partial class NPCUtilitySystem
{
    private void AddNeutralTurretTargets(EntityUid owner, float vision, HashSet<EntityUid> entities)
    {
        if (!TryComp<TurretTargetSettingsComponent>(owner, out var targetSettings) ||
            !targetSettings.TargetNeutralEntities)
        {
            return;
        }

        var mapPos = _transform.GetMapCoordinates(owner, xform: _xformQuery.GetComponent(owner));
        _entitySet.Clear();
        _lookup.GetEntitiesInRange(typeof(MobStateComponent), mapPos, vision, _entitySet);
        _lookup.GetEntitiesInRange(typeof(MechComponent), mapPos, vision, _entitySet);

        foreach (var candidate in _entitySet)
        {
            var target = candidate.Owner;
            if (target == owner ||
                !_mobState.IsAlive(target) ||
                HasComp<ActorComponent>(target) ||
                HasComp<ActiveNPCComponent>(target))
            {
                continue;
            }

            if (TryComp<NpcFactionMemberComponent>(owner, out var ownerFaction) &&
                TryComp<NpcFactionMemberComponent>(target, out var targetFaction) &&
                _npcFaction.IsEntityFriendly((owner, ownerFaction), (target, targetFaction)))
            {
                continue;
            }

            entities.Add(target);
        }
    }
}

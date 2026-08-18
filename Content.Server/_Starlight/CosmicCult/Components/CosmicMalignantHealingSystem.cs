using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.CosmicCult.EntitySystems;

public sealed partial class CosmicMalignantHealingSystem : EntitySystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobThresholdSystem _mobThresholds = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<
            CosmicMalignantHealingComponent,
            MobStateComponent,
            DamageableComponent>();

        while (query.MoveNext(out var uid, out var healing, out var mobState, out var damageable))
        {
            if (_timing.CurTime < healing.NextHeal)
                continue;

            healing.NextHeal = _timing.CurTime + healing.HealInterval;

            var xform = Transform(uid);

            if (xform.GridUid is not { } gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var mapGrid))
                continue;

            var tile = _map.GetTileRef((gridUid, mapGrid), xform.Coordinates);

            var healingTile =
                (ContentTileDefinition)_tileDefinition[healing.HealingTile];

            if (tile.Tile.TypeId != healingTile.TileId)
                continue;

            // Heal every damage type by up to HealAmount.
            var damage = new DamageSpecifier();

            foreach (var (type, amount) in damageable.Damage.DamageDict)
            {
                if (amount > 0)
                    damage.DamageDict[type] = -healing.HealAmount;
            }

            if (damage.DamageDict.Count == 0)
                continue;

            _damageable.TryChangeDamage(uid, damage, true, true);
            _mobThresholds.VerifyThresholds(uid, null, mobState, damageable);
        }
    }
}

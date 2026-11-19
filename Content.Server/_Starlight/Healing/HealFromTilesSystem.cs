using Content.Shared._Starlight.Healing;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Healing;

public sealed partial class HealFromTilesSystem : EntitySystem
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
            HealFromTilesComponent,
            MobStateComponent,
            DamageableComponent>();

        while (query.MoveNext(out var uid, out var healing, out var mobState, out var damageable))
        {
            if (_timing.CurTime < healing.NextHeal)
                continue;

            healing.NextHeal = _timing.CurTime + healing.HealInterval;

            if (healing.Tiles.Count == 0)
                continue;

            var xform = Transform(uid);

            if (xform.GridUid is not { } gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var mapGrid))
                continue;

            var tile = _map.GetTileRef((gridUid, mapGrid), xform.Coordinates);

            var isHealingTile = false;

            foreach (var tileId in healing.Tiles)
            {
                var healingTile = (ContentTileDefinition)_tileDefinition[tileId];

                if (tile.Tile.TypeId != healingTile.TileId)
                    continue;

                isHealingTile = true;
                break;
            }

            if (!isHealingTile)
                continue;

            // Heal every damage type by up to HealAmount.
            var damage = new DamageSpecifier();
            var damageSpec = _damageable.GetAllDamage(uid);

            foreach (var (type, amount) in damageSpec.DamageDict)
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

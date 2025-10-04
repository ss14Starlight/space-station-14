using Content.Shared._Starlight.Legendary;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Legendary;

public sealed class LegendaryItemSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegendaryItemComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, LegendaryItemComponent component, ref MapInitEvent args)
    {
        if (component.RollProcessed)
            return;

        component.RollProcessed = true;

        if (!TryApplyLegendary(uid, component))
        {
            RemCompDeferred<LegendaryItemComponent>(uid);
            return;
        }
    }

    internal bool TryApplyLegendary(EntityUid uid, LegendaryItemComponent component)
    {
        var chance = Math.Clamp(component.Chance, 0f, 1f);
        if (chance <= 0f || !_random.Prob(chance))
            return false;

        component.LegendaryApplied = true;

        if (component.Description != null)
        {
            var meta = MetaData(uid);
            _meta.SetEntityDescription(uid, Loc.GetString(component.Description.Value), meta);
        }

        return true;
    }
}

using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server.Store.Systems;
using Content.Shared._Starlight.Pollen.Components;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using Content.Shared._Starlight.Pollen;

namespace Content.Server._Starlight.Pollen.Systems;

public sealed partial class PollenCollectorSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private PollenCatalogSystem _catalog = default!;
    [Dependency] private StoreSystem _store = default!;

    public static readonly ProtoId<CurrencyPrototype> PollenPointsCurrency = "PollenPoints";

    private static readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);
    private static readonly ProtoId<ReagentPrototype> _omnizine = "Omnizine";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenCollectorComponent, MapInitEvent>(OnCollectorMapInit);
    }

    private void OnCollectorMapInit(Entity<PollenCollectorComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Pollen.Count > 0)
            return;

        var plants = _catalog.PollenIds.ToList();

        if (plants.Count == 0)
            Log.Warning("PollenCatalog is empty: no prototype has both EmitPollen and Produce with a seedId.");

        _random.Shuffle(plants);

        for (var i = 0; i < Math.Min(ent.Comp.PlantCount, plants.Count); i++)
        {
            ent.Comp.Pollen[plants[i]] = false;
        }

        Dirty(ent);

        var initializedEvent = new PollenCollectorInitializedEvent();
        RaiseLocalEvent(ent.Owner, initializedEvent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var collectorQuery = EntityQueryEnumerator<PollenCollectorComponent, TransformComponent>();

        while (collectorQuery.MoveNext(out var uid, out var collector, out var collectorXform))
        {
            if (now < collector.NextCollection)
                continue;

            collector.NextCollection = now + _checkInterval;

            if (collector.Collected >= collector.Pollen.Count)
                continue;

            var markerQuery = EntityQueryEnumerator<ScentMarkerComponent, TransformComponent>();
            while (markerQuery.MoveNext(out _, out var marker, out var markerXform))
            {
                if (!marker.IsPollen)
                    continue;

                if (!_transform.InRange(collectorXform.Coordinates, markerXform.Coordinates, collector.PollenRange))
                    continue;

                if (!_random.Prob(collector.InteractionChance))
                    continue;

                TryCollect((uid, collector), marker.ScentId);
            }
        }
    }

    private void TryCollect(Entity<PollenCollectorComponent> ent, string pollenId)
    {
        if (!ent.Comp.Pollen.TryGetValue(pollenId, out var done) || done)
            return;

        ent.Comp.Pollen[pollenId] = true;
        ent.Comp.Collected++;
        Dirty(ent);

        var granted = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>
        {
            { PollenPointsCurrency, ent.Comp.PointsPerPlant }
        };

        if (!_store.TryAddCurrency(granted, ent.Owner))
        {
            Log.Warning($"{ToPrettyString(ent.Owner)} collected pollen but has no Store component " +
                        "(or its CurrencyWhitelist is missing PollenPoints) - no points were granted.");
        }

        _popup.PopupEntity(
            Loc.GetString("pollen-absorbed",
                ("plant", _catalog.GetName(pollenId)),
                ("points", ent.Comp.PointsPerPlant)),
            ent.Owner,
            ent.Owner,
            PopupType.Small);
    }
}

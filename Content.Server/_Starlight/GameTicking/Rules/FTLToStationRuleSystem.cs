using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class FTLToStationRuleSystem : GameRuleSystem<FTLToStationRuleComponent>
{
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FTLToStationRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
    }

    private void OnRuleLoadedGrids(Entity<FTLToStationRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        if (!TryGetRandomStation(out var chosenStation))
            return;

        var targetGrid = _stationSystem.GetLargestGrid((chosenStation.Value, Comp<StationDataComponent>(chosenStation.Value)));
        if (targetGrid is null)
            return;

        foreach (var grid in args.Grids)
            _shuttles.FTLToDock(
                grid,
                Comp<ShuttleComponent>(grid),
                targetGrid.Value,
                0,
                ent.Comp.HyperspaceTime,
                ent.Comp.PriorityTag);
    }
}

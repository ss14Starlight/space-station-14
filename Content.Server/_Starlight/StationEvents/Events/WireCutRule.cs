using System.Linq;
using Content.Server._Starlight.StationEvents.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Server.Wires;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Random;

namespace Content.Server._Starlight.StationEvents.Events;

/// <summary>
///     Cuts a random number of wires on whitelisted devices across the target station.
/// </summary>
[UsedImplicitly]
public sealed partial class WireCutRule : StationEventSystem<WireCutRuleComponent>
{
    [Dependency] private WiresSystem _wires = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    protected override void Started(EntityUid uid, WireCutRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        var station = stationEvent.TargetStation;
        if (station == null && !TryGetRandomStation(out station))
            return;

        stationEvent.TargetStation = station;
        var stationUid = station.Value;

        var candidates = new List<(EntityUid Uid, WiresComponent Wires)>();
        var query = EntityQueryEnumerator<WiresComponent, TransformComponent>();
        while (query.MoveNext(out var target, out var wires, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != stationUid)
                continue;

            if (!_whitelist.IsWhitelistPassOrNull(component.Whitelist, target))
                continue;

            if (_whitelist.IsWhitelistPass(component.Blacklist, target))
                continue;

            if (!wires.WiresList.Exists(wire => !wire.IsCut))
                continue;

            candidates.Add((target, wires));
        }

        if (candidates.Count == 0)
            return;

        var maxWires = Math.Max(component.MinWires, component.MaxWires);
        var requested = Math.Min(RobustRandom.Next(component.MinWires, maxWires + 1), candidates.Count);
        var remaining = requested;

        RobustRandom.Shuffle(candidates);

        foreach (var (target, wires) in candidates)
        {
            if (remaining <= 0)
                break;

            if (TerminatingOrDeleted(target))
                continue;

            var uncutWires = wires.WiresList.Where(wire => !wire.IsCut).ToList();
            RobustRandom.Shuffle(uncutWires);

            var perEntity = Math.Min(Math.Max(1, component.MaxWiresPerEntity), uncutWires.Count);
            foreach (var wire in uncutWires)
            {
                if (remaining <= 0 || perEntity <= 0)
                    break;

                if (TerminatingOrDeleted(target))
                    break;

                if (!_wires.TryCutWire(target, wire, wires: wires))
                    continue;

                remaining--;
                perEntity--;
            }
        }

        Sawmill.Info($"Cut {requested - remaining} of {requested} planned wires on station {stationUid} for {ToPrettyString(uid)}");
    }
}

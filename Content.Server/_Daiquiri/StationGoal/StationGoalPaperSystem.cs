using Content.Server.Fax;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server._Daiquiri.GameTicking;
using Content.Shared._Daiquiri.CCVar;
using Content.Shared.Fax.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Daiquiri.StationGoal;

/// <summary>
///     Spawns paper with a station goal on the station fax machine at round start.
/// </summary>
public sealed partial class StationGoalPaperSystem : EntitySystem
{
    private static readonly EntProtoId DefaultGoalPrototype = "BaseStationGoal";

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FaxSystem _fax = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_cfg.GetCVar(DaiquiriCCVars.StationGoal))
            return;

        ApplyDefaultGoals();

        var playerCount = _playerManager.PlayerCount;

        var query = EntityQueryEnumerator<StationGoalComponent>();
        while (query.MoveNext(out var uid, out var station))
        {
            var tempGoals = new List<ProtoId<StationGoalPrototype>>(station.Goals);
            StationGoalPrototype? selGoal = null;
            while (tempGoals.Count > 0)
            {
                var goalId = _random.Pick(tempGoals);
                var goalProto = _proto.Index(goalId);

                if (playerCount > goalProto.MaxPlayers ||
                    playerCount < goalProto.MinPlayers)
                {
                    tempGoals.Remove(goalId);
                    continue;
                }

                selGoal = goalProto;
                break;
            }

            if (selGoal is null)
                continue;

            if (SendStationGoal(uid, selGoal))
                Log.Info($"Goal {selGoal.ID} has been sent to station {MetaData(uid).EntityName}");
        }
    }

    public bool SendStationGoal(EntityUid ent, ProtoId<StationGoalPrototype> goal)
    {
        return SendStationGoal(ent, _proto.Index(goal));
    }

    private void ApplyDefaultGoals()
    {
        if (!_proto.TryIndex(DefaultGoalPrototype, out var prototype))
            return;

        var query = EntityQueryEnumerator<BecomesStationComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<StationGoalComponent>(uid))
                continue;

            EntityManager.AddComponents(uid, prototype);
        }
    }

    /// <summary>
    ///     Sends a station goal to all faxes which are authorized to receive it.
    /// </summary>
    /// <returns>True if at least one fax received a paper.</returns>
    public bool SendStationGoal(EntityUid ent, StationGoalPrototype goal)
    {
        var printout = new FaxPrintout(
            Loc.GetString(goal.Text, ("station", MetaData(ent).EntityName)),
            Loc.GetString("station-goal-fax-paper-name"),
            null,
            null,
            "paper_stamp-centcom",
            [new() { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#006600") }]
        );

        var wasSent = false;
        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var faxUid, out var fax))
        {
            if (!fax.ReceiveAllStationGoals && !(fax.ReceiveStationGoal && _station.GetOwningStation(faxUid) == ent))
                continue;

            _fax.Receive(faxUid, printout, null, fax);

            foreach (var spawnEnt in goal.Spawns)
                SpawnAtPosition(spawnEnt, Transform(faxUid).Coordinates);

            wasSent |= fax.ReceiveStationGoal;
        }

        return wasSent;
    }
}

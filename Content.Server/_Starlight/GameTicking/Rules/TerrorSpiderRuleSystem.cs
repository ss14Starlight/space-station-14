using Content.Server._Starlight.Antags.Components;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed class TerrorSpiderRuleSystem : GameRuleSystem<TerrorSpiderRuleComponent>
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;

    /// <summary>
    /// How much of the crew needs to be dead for the spiders to win.
    /// </summary>
    private const int TargetDeadCrewPercentage = 50;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationCrewComponent, MobStateChangedEvent>(OnCrewMobStateChanged);
    }

    private void OnCrewMobStateChanged(EntityUid uid, StationCrewComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Dead or MobState.Invalid)
            CheckLoseStatus();
    }

    private bool CheckLoseStatus()
    {
        var crewList = new List<EntityUid>();

        var crew = EntityQueryEnumerator<StationCrewComponent>();
        while (crew.MoveNext(out var uid, out _))
            crewList.Add(uid);

        var crewDeadAmount = CheckGroupStatus(crewList);
        return crewDeadAmount * 100 / crewList.Count >= TargetDeadCrewPercentage;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="crewList"></param>
    /// <param name="checkOffStation"></param>
    /// <returns></returns>
    private int CheckGroupStatus(IEnumerable<EntityUid> entities, bool checkOffStation = true)
    {
        var gone = 0;
        foreach (var ent in entities)
        {
            if (EntityManager.TryGetComponent(ent, out MobStateComponent? mobState) && mobState.CurrentState is MobState.Dead or MobState.Invalid)
                gone++;
            else if (checkOffStation && _stationSystem.GetOwningStation(ent) == null && !_emergencyShuttle.EmergencyShuttleArrived)
                gone++;
        }
        return gone;
    }
}

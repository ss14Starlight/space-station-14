using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Shared.Localizations;
using Robust.Server.GameObjects;

namespace Content.Server._Starlight.GameTicking.Rules;

/// <summary>
/// Configurable briefing system for antag game rules.
/// </summary>
public sealed partial class BriefingRuleSystem : GameRuleSystem<BriefingRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BriefingRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
    }

    private void OnAfterAntagEntitySelected(Entity<BriefingRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var briefing = MakeBriefing(args.EntityUid, ent.Comp.Briefing);
        _antag.SendBriefing(args.EntityUid, briefing, ent.Comp.Color, ent.Comp.Sound);
    }

    private string MakeBriefing(EntityUid uid, string briefing)
    {
        var direction = GetStationDirection(uid);

        return Loc.GetString(
            briefing,
            ("direction", direction));
    }

    private string GetStationDirection(EntityUid uid)
    {
        var xform = Transform(uid);

        if (_station.GetStationInMap(xform.MapID) is not { } station)
            return string.Empty;

        var stationGrid = _station.GetLargestGrid(station);

        if (stationGrid is null)
            return string.Empty;

        var stationPosition = _transform.GetWorldPosition(stationGrid.Value);
        var entPosition = _transform.GetWorldPosition(uid);
        var vectorToStation = stationPosition - entPosition;

        return ContentLocalizationManager.FormatDirection(vectorToStation.GetDir());
    }
}

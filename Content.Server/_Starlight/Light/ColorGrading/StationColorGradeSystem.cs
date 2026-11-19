using Content.Server.AlertLevel;
using Content.Shared._Starlight.Light.ColorGrading;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Light.ColorGrading;

/// <summary>
/// Hands the colour grade of a station's alert level to clients through <see cref="StationColorGradeComponent"/>
/// on each of its grids.
/// </summary>
public sealed partial class StationColorGradeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    [SubscribeLocalEvent]
    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        if (!TryComp<StationDataComponent>(args.Station, out var data))
            return;

        ProtoId<ColorGradePrototype>? grade = null;
        if (_proto.TryIndex<AlertColorGradesPrototype>(AlertColorGradesPrototype.DefaultId, out var grades)
            && grades.Levels.TryGetValue(args.AlertLevel, out var levelGrade))
        {
            grade = levelGrade;
        }

        foreach (var grid in data.Grids)
        {
            if (grade == null && !HasComp<StationColorGradeComponent>(grid))
                continue;

            var comp = EnsureComp<StationColorGradeComponent>(grid);
            if (comp.AlertGrade == grade)
                continue;

            comp.AlertGrade = grade;
            Dirty(grid, comp);
        }
    }
}

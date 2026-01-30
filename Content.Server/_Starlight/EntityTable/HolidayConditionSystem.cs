using Content.Server.Holiday;
using Content.Shared._Starlight.EntityTable;

namespace Content.Server._Starlight.EntityTable;

public sealed partial class HolidayConditionSystem : EntitySystem
{
    [Dependency] private readonly HolidaySystem _holiday = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Populate early
        _holiday.RefreshCurrentHolidays();
        SubscribeLocalEvent<HolidayConditionCheckEvent>(OnHolidayCheck);
    }

    private void OnHolidayCheck(HolidayConditionCheckEvent ev)
    {
        var valid = _holiday.IsCurrentlyHoliday(ev.Holiday);
        ev.Valid = valid;
    }
}

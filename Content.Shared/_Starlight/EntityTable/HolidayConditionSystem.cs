using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.EntityTable;

public sealed partial class HolidayConditionSystem : EntitySystem
{
    public bool CheckHoliday(string holiday)
    {
        var check = new HolidayConditionCheckEvent(holiday);
        RaiseLocalEvent(check);
        return check.Valid;
    }
}

[Serializable, NetSerializable]
public sealed class HolidayConditionCheckEvent(string holiday) : EntityEventArgs
{
    public string Holiday = holiday;
    public bool Valid = false;
}

// ReSharper disable CheckNamespace
namespace Content.Client.Administration.UI.Notes;

partial class NoteEdit
{
    private DateTime? CreationTime { get; set; }

    private static (uint Number, Multipliers Unit) UnparseExpiryTime(DateTime createdTime, DateTime expiryTime)
    {
        TimeSpan duration = expiryTime - createdTime;
        long durationTicks = duration.Ticks;
        long toleranceTicks = durationTicks / 100; // 1% tolerance

        uint number = 0;
        bool Check(TimeSpan unit)
        {
            long unitTicks = unit.Ticks;
            long mod = durationTicks % unitTicks;
            long errorRoundDown = mod, errorRoundUp = unitTicks - mod;
            number = (uint)(durationTicks / unitTicks);
            number = errorRoundDown < errorRoundUp ? number : number + 1;
            return long.Min(errorRoundDown, errorRoundUp) <= toleranceTicks;
        }

        if (Check(TimeSpan.FromDays(36525))) return (number, Multipliers.Centuries);
        if (Check(TimeSpan.FromDays(365))) return (number, Multipliers.Years);
        if (Check(TimeSpan.FromDays(30))) return (number, Multipliers.Months);
        if (Check(TimeSpan.FromDays(7))) return (number, Multipliers.Weeks);
        if (Check(TimeSpan.FromDays(1))) return (number, Multipliers.Days);
        if (Check(TimeSpan.FromHours(1))) return (number, Multipliers.Hours);
        Check(TimeSpan.FromMinutes(1));
        return (number, Multipliers.Minutes);
    }
}

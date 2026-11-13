// ReSharper disable CheckNamespace
namespace Content.Client.Administration.UI.Notes;

partial class NoteEdit
{
    private DateTime? _creationTime { get; set; }

    private static (int Number, Multipliers Unit) UnparseExpiryTime(DateTime createdTime, DateTime expiryTime)
    {
        TimeSpan duration = expiryTime - createdTime;
        long durationTicks = long.Abs(duration.Ticks);
        long toleranceTicks = durationTicks / 1000; // 0.1% tolerance

        int number = 0;
        bool Check(TimeSpan unit)
        {
            long unitTicks = unit.Ticks;
            long mod = durationTicks % unitTicks;
            long errorRoundDown = mod, errorRoundUp = unitTicks - mod;
            number = (int)(durationTicks / unitTicks);
            number = errorRoundDown < errorRoundUp ? number : number + 1;
            number *= long.Sign(toleranceTicks);
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

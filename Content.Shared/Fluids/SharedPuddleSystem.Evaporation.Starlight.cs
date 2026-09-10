using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared.Fluids;

public abstract partial class SharedPuddleSystem
{
    private void ScheduleEvaporation(TimeSpan time)
    {
        if (time < _nextEvaporationUpdate)
            _nextEvaporationUpdate = time;
    }

    private bool HasEvaporatingReagent(Solution solution)
    {
        foreach (var (reagent, _) in solution.Contents)
        {
            if (_prototypeManager.Index<ReagentPrototype>(reagent.Prototype).EvaporationSpeed > FixedPoint2.Zero)
                return true;
        }

        return false;
    }
}

using Content.Shared.Atmos.Rotting;

namespace Content.Shared.Atmos.Rotting;

public abstract partial class SharedRottingSystem
{
    public bool SetRotAfter(EntityUid uid, TimeSpan newTime, PerishableComponent? perishable = null)
    {
        if (!Resolve(uid, ref perishable))
            return false;

        perishable.RotAfter = newTime;
        return true;
    }
}
using Content.Shared.Mech.EntitySystems;

namespace Content.Shared.MouseRotator;

public abstract partial class SharedMouseRotatorSystem
{
    [Dependency] private SharedMechSystem _mech = default!;

    private bool TryGetMech(EntityUid pilot, out EntityUid mech)
    {
        mech = default;
        if (!_mech.TryGetPilotMech(pilot, out var piloted))
            return false;
        mech = piloted.Owner;
        return true;
    }
}

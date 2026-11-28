using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.CombatMode;

public abstract partial class SharedCombatModeSystem
{
    [Dependency] private SharedMechSystem _mech = default!;

    private void SetMechRotation(EntityUid pilot, bool enabled)
    {
        if (!_mech.TryGetPilotMech(pilot, out var mech))
            return;
        if (enabled)
            EnsureComp<NoRotateOnMoveComponent>(mech);
        else
            RemComp<NoRotateOnMoveComponent>(mech);
    }
}

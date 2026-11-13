using System.Numerics;
using Content.Shared.Throwing;

namespace Content.Shared.Doors.Systems;

public abstract partial class SharedDoorSystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    private Vector2 GetEjectDirection(EntityUid victim) => -_xform.GetWorldRotation(victim).ToWorldVec();
}

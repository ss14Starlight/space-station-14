using System.Numerics;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Doors.Systems;

public abstract partial class SharedDoorSystem
{
    public static readonly ProtoId<TagPrototype> DecorativeDoorTag = "DecorativeDoor";
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    private Vector2 GetEjectDirection(EntityUid victim) => -_xform.GetWorldRotation(victim).ToWorldVec();
}

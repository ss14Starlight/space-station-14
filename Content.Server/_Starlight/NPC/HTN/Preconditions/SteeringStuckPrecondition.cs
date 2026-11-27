using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Robust.Shared.Physics.Components;
 
namespace Content.Server._Starlight.NPC.HTN.Preconditions;
 
/// <summary>
/// True when the owner is steering somewhere but is not actually moving, (aka stuck).
/// </summary>
public sealed partial class SteeringStuckPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;
 
    /// <summary>Speed in tiles per second below which the NPC counts as not moving.</summary>
    [DataField("maxSpeed")]
    public float MaxSpeed = .7f;
 
    [DataField]
    public bool Invert;
 
    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
 
        if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
            return Invert;
 
        var stuck = false;
 
        if (steering.Status == SteeringStatus.NoPath)
        {
            stuck = true;
        }
        else if (steering.Status == SteeringStatus.Moving &&
                 _entManager.TryGetComponent<PhysicsComponent>(owner, out var body))
        {
            stuck = body.LinearVelocity.LengthSquared() < MaxSpeed * MaxSpeed;
        }
 
        return stuck ^ Invert;
    }
}
 
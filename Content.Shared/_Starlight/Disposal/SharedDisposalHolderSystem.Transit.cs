using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Disposal.Holder;

public abstract partial class SharedDisposalHolderSystem
{
    /// <summary>
    /// Stops or resumes a holder in place.
    /// </summary>
    public void SetHalted(Entity<DisposalHolderComponent> ent, bool halted)
    {
        if (ent.Comp.Halted == halted)
            return;

        ent.Comp.Halted = halted;

        if (halted)
            _physics.SetLinearVelocity(ent, System.Numerics.Vector2.Zero);

        Dirty(ent);
    }

    /// <summary>
    /// Changes how fast a holder moves, in tiles per second.
    /// </summary>
    public void SetTraversalSpeed(Entity<DisposalHolderComponent> ent, float speed)
    {
        ent.Comp.TraversalSpeed = speed;
    }

    /// <summary>
    /// Removes every routing tag from a holder.
    /// </summary>
    public void ClearTags(Entity<DisposalHolderComponent> ent)
    {
        if (ent.Comp.Tags.Count == 0)
            return;

        ent.Comp.Tags.Clear();
        Dirty(ent);
    }

    /// <summary>
    /// Sends a holder out of a tube in the given direction.
    /// </summary>
    /// <returns>False if there is no connected tube in that direction.</returns>
    public bool Launch(Entity<DisposalHolderComponent> ent, Entity<DisposalTubeComponent> tube, Direction direction)
    {
        var next = _disposalTube.GetTubeInDirection(tube, direction);
        if (next == null)
            return false;

        ent.Comp.CurrentTube = tube;
        ent.Comp.NextTube = next;
        ent.Comp.CurrentDirection = direction;
        ent.Comp.IsExiting = false;
        ent.Comp.Halted = false;
        _xform.SetLocalRotation(ent, direction.ToAngle());
        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Turns a moving holder around, sending it back to the tube it came from.
    /// </summary>
    public bool Reverse(Entity<DisposalHolderComponent> ent)
    {
        if (ent.Comp.CurrentTube is not { } current || ent.Comp.NextTube is not { } next)
            return false;

        ent.Comp.CurrentTube = next;
        ent.Comp.NextTube = current;
        ent.Comp.CurrentDirection = ent.Comp.CurrentDirection.GetOpposite();
        _xform.SetLocalRotation(ent, ent.Comp.CurrentDirection.ToAngle());
        Dirty(ent);
        return true;
    }
}

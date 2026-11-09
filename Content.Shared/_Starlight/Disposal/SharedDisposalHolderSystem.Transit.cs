using Content.Shared.Disposal.Tube;
using Content.Shared.Disposal.Unit;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Disposal.Holder;

public abstract partial class SharedDisposalHolderSystem
{
    /// <summary>
    /// Stops or resumes a holder in place.
    /// </summary>
    public void SetHalted(Entity<DisposalHolderComponent?> ent, bool halted)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Halted == halted)
            return;

        ent.Comp.Halted = halted;

        if (halted)
            _physics.SetLinearVelocity(ent, System.Numerics.Vector2.Zero);

        Dirty(ent);
    }

    /// <summary>
    /// Changes how fast a holder moves, in tiles per second.
    /// </summary>
    public void SetTraversalSpeed(Entity<DisposalHolderComponent?> ent, float speed)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (MathHelper.CloseTo(ent.Comp.TraversalSpeed, speed))
            return;

        ent.Comp.TraversalSpeed = speed;
        Dirty(ent);
    }

    /// <summary>
    /// Removes every routing tag from a holder.
    /// </summary>
    public void ClearTags(Entity<DisposalHolderComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.Tags.Count == 0)
            return;

        ent.Comp.Tags.Clear();
        Dirty(ent);
    }

    /// <summary>
    /// Sends a holder out of a tube in the given direction.
    /// </summary>
    /// <returns>False if the tube has no exit that way, or no connected tube there.</returns>
    public bool Launch(Entity<DisposalHolderComponent?> ent, Entity<DisposalTubeComponent?> tube, Direction direction)
    {
        if (!Resolve(ent, ref ent.Comp) || !Resolve(tube, ref tube.Comp))
            return false;

        // Only through a side the tube is actually open on.
        if (Array.IndexOf(_disposalTube.GetTubeConnectableDirections((tube, tube.Comp)), direction) < 0)
            return false;

        var next = _disposalTube.GetTubeInDirection((tube, tube.Comp), direction);
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
    public bool Reverse(Entity<DisposalHolderComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp)
            || ent.Comp.CurrentTube is not { } current
            || ent.Comp.NextTube is not { } next)
            return false;

        ent.Comp.CurrentTube = next;
        ent.Comp.NextTube = current;
        ent.Comp.CurrentDirection = ent.Comp.CurrentDirection.GetOpposite();
        _xform.SetLocalRotation(ent, ent.Comp.CurrentDirection.ToAngle());
        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Forgets the tubes a holder was in, for when they are gone from under it.
    /// </summary>
    public void ClearTubes(Entity<DisposalHolderComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp) || (ent.Comp.CurrentTube == null && ent.Comp.NextTube == null))
            return;

        ent.Comp.CurrentTube = null;
        ent.Comp.NextTube = null;
        Dirty(ent);
    }
}

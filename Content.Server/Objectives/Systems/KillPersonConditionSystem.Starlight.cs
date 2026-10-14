using Content.Shared.Body.Components;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;

namespace Content.Server.Objectives.Systems;

public sealed partial class KillPersonConditionSystem
{
    /// <summary>
    /// Reuses elimination's early removal and evacuation rules for composite objectives.
    /// </summary>
    public float GetEliminationProgress(EntityUid target)
        => GetProgress(target, requireDead: false, requireMaroon: true);

    private bool IsTargetRemoved(MindComponent mind)
    {
        if (mind.OwnedEntity is not { } body || TerminatingOrDeleted(body))
            return true;
        if (HasComp<CryostorageContainedComponent>(body))
            return true;

        // Disconnecting keeps UserId, and a visiting ghost keeps ownership of the recoverable body.
        if ((mind.UserId == null && mind.OriginalOwnerUserId != null) ||
            (TryComp<MindExaminableComponent>(body, out var examine) &&
            examine.State is MindState.Catatonic or MindState.Irrecoverable))
            return true;
        if (TryComp<GhostComponent>(body, out var ghost))
            return !ghost.CanReturnToBody;
        return SLIsCharacterUnrevivable(mind);
    }

    private bool SLIsCharacterUnrevivable(MindComponent mind)
    {
        // BrainSystem can transfer a detached organic brain back into a body. Borging or refusing
        // its ghost role transfers ownership to the chassis/ghost, so those still count as removed.
        if (mind.OwnedEntity is { } body && HasComp<BrainComponent>(body) && !TerminatingOrDeleted(body))
            return false;
        return _mind.IsCharacterUnrevivableIc(mind);
    }
}

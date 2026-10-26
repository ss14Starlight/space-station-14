using Content.Shared._Starlight.Medical.Body.Events;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    /// <summary>
    /// Checks the policy for a mind transfer caused by inserting or removing an organ.
    /// </summary>
    public bool CanTransferMindOnOrganChange(EntityUid source, EntityUid target)
    {
        if (TerminatingOrDeleted(source) || TerminatingOrDeleted(target))
            return false;

        var attempt = new BodyMindTransferAttemptEvent(source, target);
        RaiseLocalEvent(source, ref attempt);
        RaiseLocalEvent(target, ref attempt);
        return !attempt.Cancelled;
    }
}

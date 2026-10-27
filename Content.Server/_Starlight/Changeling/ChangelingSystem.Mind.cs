using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Events;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    /// <summary>
    /// Keeps the changeling's mind in its body when body-part or organ changes request a transfer.
    /// This also prevents an implanted head or brain from bringing another mind into the body.
    /// </summary>
    /// <remarks>
    /// Only anatomical transfers raise this event; direct mind transfers such as magical swaps remain available.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnBodyMindTransferAttempt(Entity<ChangelingComponent> ent, ref BodyMindTransferAttemptEvent args)
        => args.Cancelled = true;
}

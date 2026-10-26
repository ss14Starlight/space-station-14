using Content.Shared._Starlight.Changeling;
using Content.Shared._Starlight.Medical.Body.Events;

namespace Content.Server._Starlight.Changeling;

public sealed partial class ChangelingSystem
{
    [SubscribeLocalEvent]
    private void OnBodyMindTransferAttempt(Entity<ChangelingComponent> ent, ref BodyMindTransferAttemptEvent args)
        => args.Cancelled = true;
}

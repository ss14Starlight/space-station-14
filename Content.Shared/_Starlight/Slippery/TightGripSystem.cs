using Content.Shared.Standing;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.Slippery;

public sealed partial class TightGripSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FellDownThrowAttemptEvent>(OnFellDownThrowAttempt);
    }

    private void OnFellDownThrowAttempt(ref FellDownThrowAttemptEvent args)
    {
        if (HasComp<TightGripComponent>(args.Thrower))
            args.Cancelled = true;
    }
}

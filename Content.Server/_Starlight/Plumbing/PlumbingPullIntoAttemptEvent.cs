using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.Server._Starlight.Plumbing;

/// <summary>
///     Raised on the DESTINATION (puller) entity when a reagent is about to be pulled into it.
///     Handlers can reduce <see cref="MaxAllowed"/> to cap the pull amount,
///     or set <see cref="Cancelled"/> to deny the pull entirely.
/// </summary>
[ByRefEvent]
public struct PlumbingPullIntoAttemptEvent
{
    /// <summary>
    ///     The source entity being pulled from.
    /// </summary>
    public EntityUid Source;

    /// <summary>
    ///     The reagent prototype ID being pulled.
    /// </summary>
    public string ReagentPrototype;

    /// <summary>
    ///     The amount the system wants to pull. Read-only reference value.
    /// </summary>
    public FixedPoint2 Amount;

    /// <summary>
    ///     Maximum amount the destination will accept. Handlers can lower this
    ///     (but not raise it above <see cref="Amount"/>). Defaults to <see cref="Amount"/>.
    /// </summary>
    public FixedPoint2 MaxAllowed;

    /// <summary>
    ///     Set to true to fully deny pulling this reagent.
    /// </summary>
    public bool Cancelled;

    public PlumbingPullIntoAttemptEvent(EntityUid source, string reagentPrototype, FixedPoint2 amount)
    {
        Source = source;
        ReagentPrototype = reagentPrototype;
        Amount = amount;
        MaxAllowed = amount;
        Cancelled = false;
    }
}

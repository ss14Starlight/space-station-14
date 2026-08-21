using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Controls if the game should run station events
    /// </summary>
    [CVarControl(AdminFlags.Server | AdminFlags.Mapping)]
    public static readonly CVarDef<bool>
        EventsEnabled = CVarDef.Create("events.enabled", true, CVar.ARCHIVE | CVar.SERVERONLY);

    /// <summary>
    ///     Multiplier applied to an event's weight for each time it has already run this round.
    ///     An event that has run twice is weighted <c>falloff ^ 2</c> of its configured weight,
    ///     so repeats become progressively less likely without ever being ruled out.
    /// </summary>
    /// <remarks>
    ///     Defaults to 1, which keeps the stock behaviour of weighting purely by the configured
    ///     weight. Lower it (0.6 is a reasonable starting point) if repeated events feel too
    ///     common; this is deliberately opt-in so it does not silently change how existing
    ///     servers pick events. Values are clamped to 0..1.
    /// </remarks>
    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<float>
        EventsRepetitionFalloff = CVarDef.Create("events.repetition_falloff", 1f, CVar.SERVERONLY);
}

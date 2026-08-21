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
    ///     Weighted selection on its own has no memory, so the same event can be drawn several
    ///     times in a row purely by chance. 0.6 matches the repetition penalty long used by
    ///     storyteller-style schedulers in other codebases. Set to 1 to disable the penalty and
    ///     weight purely by the configured weight. Values are clamped to 0..1.
    /// </remarks>
    [CVarControl(AdminFlags.Server)]
    public static readonly CVarDef<float>
        EventsRepetitionFalloff = CVarDef.Create("events.repetition_falloff", 0.6f, CVar.ARCHIVE | CVar.SERVERONLY); // Starlight
}

namespace Content.Server.Body.Components;

public sealed partial class RespiratorComponent
{
    /// <summary>
    /// Whether the respiratory loop and gas exchange are enabled.
    /// </summary>
    /// <remarks>
    /// When false, scheduled saturation loss, gasps, and suffocation processing are skipped;
    /// inhaling and exhaling do not exchange gas. Disabling does not clear saturation, lung gases,
    /// or the current breath phase. Use <c>RespiratorSystem.SetEnabled</c> for runtime changes so the next
    /// update and suffocation warning state are adjusted as well.
    /// </remarks>
    [DataField]
    public bool Enabled = true;
}

namespace Content.Server.GameTicking.Presets;

public sealed partial class GamePresetPrototype
{
    /// <summary>
    /// Ignores incoming preset and rule cooldowns for this preset and its round.
    /// Its selected rules still apply cooldowns to subsequent rounds.
    /// </summary>
    [DataField]
    public bool BypassCooldowns;
}

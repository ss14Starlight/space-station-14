namespace Content.Server._Starlight.GameTicking.Rules;

[RegisterComponent, Access(typeof(NanoChatSpamRuleSystem))]
public sealed partial class NanoChatSpamRuleComponent : Component
{
    /// <summary>
    /// Maximum number of recipients per spam message.
    /// </summary>
    [DataField]
    public int MaxRecipientsPerMessage = 3;

    /// <summary>
    /// Chance (0-1) that a player with a PDA will receive any given spam message.
    /// </summary>
    [DataField]
    public float RecipientChance = 0.3f;
}

using Content.Server._Starlight.GameTicking.Rules;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(NanoChatSpamRuleSystem))]
public sealed partial class NanoChatSpamRuleComponent : Component
{
    /// <summary>
    /// Minimum delay between spam messages (in seconds).
    /// </summary>
    [DataField]
    public float MinDelay = 120f;

    /// <summary>
    /// Maximum delay between spam messages (in seconds).
    /// </summary>
    [DataField]
    public float MaxDelay = 300f;

    /// <summary>
    /// Time until next spam message.
    /// </summary>
    [DataField]
    public float NextSpamTime;

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

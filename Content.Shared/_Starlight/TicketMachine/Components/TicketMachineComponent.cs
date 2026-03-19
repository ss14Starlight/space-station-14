using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.TicketMachine.Components;

/// <summary>
/// Defines a ticket issued by a ticket machine and his number.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class TicketMachineComponent : Component
{
    #region States
    /// <summary>
    /// Current ticket number to be displayed.
    /// </summary>
    [DataField]
    public int displayNumber = 0;

    /// <summary>
    /// Whether the ticket machine has paper.
    /// </summary>
    [DataField]
    public bool hasPaper = true;

    /// <summary>
    /// Last issued ticket number. Will be reset to 0 when paper for tickets replaced.
    /// </summary>
    [DataField]
    public int lastIssuedNumber = 0;

    /// <summary>
    /// Maximum number of tickets that can be issued.
    /// </summary>
    [DataField]
    public int maxTickets = 999;

    /// <summary>
    /// Whether dispensing tickets is enabled.
    /// </summary>
    [DataField]
    public bool dispenseEnabled = true;

    /// <summary>
    /// Prototype ID of the ticket to be issued.
    /// </summary>
    [DataField]
    public EntProtoId TicketProtoId = "Ticket";

    /// <summary>
    /// Prototype ID of the refill paper item.
    /// </summary>
    [DataField]
    public string PaperContainerId = "TicketMachinePaper";

    /// <summary>
    /// List of currently issued tickets, required to be able to burn them out.
    /// </summary>
    [AutoNetworkedField, DataField]
    public List<EntityUid> issuedTickets = new();
    #endregion

    #region Cooldown

    /// <summary>
    /// Cooldown between issuing tickets.
    /// </summary>
    [DataField]
    public TimeSpan issueCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The last time a ticket was issued.
    /// </summary>
    [AutoNetworkedField, AutoPausedField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan previousIssueTime = TimeSpan.Zero;

    #endregion

    #region Visuals/Sounds

    /// <summary>
    /// Sound played when dispensing a ticket.
    /// </summary>
    [DataField]
    public SoundSpecifier dispenseSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>
    /// Sound played when dispensing a ticket.
    /// </summary>
    [DataField]
    public SoundSpecifier accessDeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    /// <summary>
    /// Tag prefix for display states. example: "display_1", "display_2", ...
    /// </summary>
    [DataField]
    public string? displayStateTag = "display_";

    /// <summary>
    /// Tag prefix for paper states. example: "paper_empty", "paper_1", "paper_2", ...
    /// </summary>
    [DataField]
    public string? paperStateTag = "paper_";

    /// <summary>
    /// Number of paper states (excluding empty). Used to calculate which state to use when updating the sprite.
    /// </summary>
    [DataField]
    public int paperStateAmount = 3;
    #endregion

    #region Device Linking
    /// <summary>
    /// Determines which port is used for receive signals to set the next display number.
    /// </summary>
    [DataField]
    public string NextNumberPort = "TicketMachineNextNumber";

    /// <summary>
    /// Determines which port is used for receive signals to burn all issued tickets.
    /// </summary>
    [DataField]
    public string BurnPort = "TicketMachineBurnTickets";
    #endregion
}

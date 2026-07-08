using Content.Server.RoundEnd;
namespace Content.Server._Starlight.GameTicking.Rules.Components;

/// <summary>
/// Component for the TerrorSpiderRuleSystem that stores info about winning/losing, player counts required for starting.
/// </summary>
[RegisterComponent, Access(typeof(TerrorSpiderRuleSystem))]
public sealed partial class TerrorSpiderRuleComponent : Component
{
    /// <summary>
    /// What happens if all of the terror spiders die.
    /// </summary>
    [DataField]
    public RoundEndBehavior RoundEndBehavior = RoundEndBehavior.ShuttleCall;

    /// <summary>
    /// Time for emergency shuttle arrival.
    /// </summary>
    [DataField]
    public TimeSpan EvacShuttleTime = TimeSpan.FromMinutes(8);

    /// <summary>
    /// Sender for shuttle call.
    /// </summary>
    [DataField]
    public LocId RoundEndTextSender = "comms-console-announcement-title-centcom";

    /// <summary>
    /// Text for shuttle call if Terror Spiders lose.
    /// </summary>
    [DataField]
    public LocId RoundEndTextShuttleCallLose = "terror-spiders-crew-victory-announcement";

    /// <summary>
    /// Text for announcement if Terror Spiders are defeated while Evac is already on its way.
    /// </summary>
    [DataField]
    public LocId RoundEndTextAnnouncementLose = "terror-spiders-elimination-announcement";

    /// <summary>
    /// Text for shuttle call if Terror Spiders win.
    /// </summary>
    [DataField]
    public LocId RoundEndTextShuttleCallWin = "terror-spiders-spider-victory-announcement";

    /// <summary>
    /// Text for announcement if Terror Spiders are victorious while Evac is already on its way.
    /// </summary>
    [DataField]
    public LocId RoundEndTextAnnouncementWin = "terror-spiders-domination-announcement";

    /// <summary>
    /// The amount of time between each check for players check.
    /// </summary>
    [DataField]
    public TimeSpan TimerWait = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How much percentage of crew should be alive to run this rule.
    /// </summary>
    [DataField]
    public float MinAliveCrewPercentage = 60;

    public TerrorSpidersWinStatus Status = TerrorSpidersWinStatus.Lose;

    [DataField]
    public TimeSpan AnnouncementDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan RoundEndDelay = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Determines how much dead crew will influence on score.
    /// </summary>
    [DataField]
    public float DeadCrewWeight = 0.7f;

    /// <summary>
    /// Determines how much spider amount will influence on score.
    /// </summary>
    [DataField]
    public float SpiderAmountWeight = 0.3f;

    /// <summary>
    /// How much of the score needs to be for the spiders to win.
    /// </summary>
    [DataField]
    public float TargetWinScore = 70f;

    /// <summary>
    /// How much of the score needs to be for the spiders to have minor win.
    /// </summary>
    [DataField]
    public float TargetMinorScore = 40f;

    /// <summary>
    /// How much spiders should have to win.
    /// </summary>
    [DataField]
    public int MinSpidersCountForWin = 15;

    [DataField]
    public bool LoseProcessed = false;

    public TimeSpan AnnouncementTime = TimeSpan.Zero;
    public bool AlreadyAnnounced = false;

    public TimeSpan EndRoundTime = TimeSpan.Zero;
    public bool RoundAlreadyEnded = false;
}

public enum TerrorSpidersWinStatus
{
    Lose,
    MinorLose,
    MinorWin,
    Win
}

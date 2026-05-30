using Content.Server.StationEvents.Events;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(SecurityDrillRule))]
public sealed partial class SecurityDrillRuleComponent : Component
{
    [DataField]
    public string RequiredAlertLevel = "green";

    [DataField]
    public float BasicDrillChance = 0.2f;

    [DataField]
    public float DetainChance = 0.3f;

    [DataField]
    public LocId FailAnnouncement = "security-drill-event-fail-announcement";

    [DataField]
    public LocId BasicDrillLocKey = "security-drill-basic";

    [DataField]
    public List<LocId> BasicDrillVariants =
    [
        "security-drill-basic-1",
        "security-drill-basic-2",
        "security-drill-basic-3",
        "security-drill-basic-4",
    ];

    [DataField]
    public LocId DetainLocKey = "security-drill-detain";

    [DataField]
    public LocId QuestioningLocKey = "security-drill-questioning";

    [DataField]
    public List<LocId> QuestioningVariants =
    [
        "security-drill-questioning-1",
        "security-drill-questioning-2",
        "security-drill-questioning-3",
        "security-drill-questioning-4",
        "security-drill-questioning-5",
    ];
}

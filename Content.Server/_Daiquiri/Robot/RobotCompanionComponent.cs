namespace Content.Server._Daiquiri.Robot;

[RegisterComponent]
public sealed partial class RobotCompanionComponent : Component
{
    [DataField]
    public float HearingRange = 5f;

    [DataField]
    public bool RespondToSpeech = true;

    [DataField]
    public bool AllowFollowOrders = true;

    [DataField]
    public float MaxFollowDistance = 30f;

    [DataField]
    public string SystemPrompt = string.Empty;

    [DataField]
    public string PersonaPrompt = string.Empty;

    [DataField]
    public bool AlwaysFemale;

    [DataField]
    public bool ReportBattery = true;

    [DataField]
    public float ChargeSearchRange = 10f;

    [DataField]
    public float ChargeEnterDistance = 1.5f;

    [DataField]
    public float MaxStepTiles = 5f;
}

using Content.Shared.FixedPoint; // Starlight
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
/// On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public HealthAnalyzerUiState State;

    public HealthAnalyzerScannedUserMessage(HealthAnalyzerUiState state)
    {
        State = state;
    }
}

// Starlight-start: Printable health reports.
[Serializable, NetSerializable]
public sealed class HealthAnalyzerPrintReportMessage : BoundUserInterfaceMessage
{
}
// Starlight-end

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public struct HealthAnalyzerUiState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? CanPrint; // Starlight-edit: Printable health reports.
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    public List<(string ReagentId, FixedPoint2 Quantity)>? MetabolizingReagents; // Starlight - list of metabolizing reagents inside scanned user

    public HealthAnalyzerUiState() {}

    public HealthAnalyzerUiState(NetEntity? targetEntity, float temperature, float bloodLevel, bool? canPrint, bool? scanMode, bool? bleeding, bool? unrevivable, List<(string ReagentId, FixedPoint2 Quantity)>? metabolizingReagents = null) // Starlight - added metabolizingReagents parameter
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        CanPrint = canPrint; // Starlight-edit: Printable health reports.
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        MetabolizingReagents = metabolizingReagents; // Starlight
    }
}

using Content.Shared.FixedPoint; // Starlight
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
///     On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    public List<(string ReagentId, FixedPoint2 Quantity)>? MetabolizingReagents; // Starlight - list of metabolizing reagents inside scanned user

    public HealthAnalyzerScannedUserMessage(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, List<(string ReagentId, FixedPoint2 Quantity)>? metabolizingReagents = null) // Starlight - added metabolizingReagents parameter
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        MetabolizingReagents = metabolizingReagents; // Starlight
    }
}


using System.Linq;
using Content.Shared.Alert;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Silicons.IPC.Components;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class FanMode
{
    [DataField(required: true)]
    public float MinTemp;

    [DataField(required: true)]
    public float BodyHeatEffect;

    [DataField(required: true)]
    public float AtmosHeatEffect;

    [DataField(required: true)]
    public LocId ExamineText;

    [DataField(required: true)]
    public LocId DiagnosticsText;

    [DataField(required: true)]
    public TimeSpan StaysOnFor;

    [DataField]
    public ProtoId<AlertPrototype>? ModeAlert = null;

}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IPCThermalRegulationComponent : Component
{
    [DataField(required: true)]
    public float ProduceHeat;

    [DataField(required: true)] public float RadiateHeatEfficiency;

    [DataField(required: true)]
    public float MinEffectivePressure;
    [DataField(required: true)]
    public float MaxEffectivePressure;
    [DataField(required: true)]
    public float MinPressure;
    [DataField(required: true)]
    public float MaxPressure;

    [DataField(required: true)]
    public float MaxTemperature;

    [DataField]
    public HashSet<FanMode> FanModes = [];

    [AutoNetworkedField, ViewVariables]
    public bool FansOff = false;
    [AutoNetworkedField, ViewVariables]
    public bool FansOffOverride = false;

    public bool FansCurrentlyOff
    {
        get => FansOff || FansOffOverride;
        set => FansOff = value;
    }

    public TimeSpan CanSwitchModeIn = TimeSpan.Zero;

    [DataField]
    public LocId FansOffExamineText = "ipc-thermals-examine-off";
    [DataField]
    public LocId FansOffDiagnosticsText = "ipc-thermals-diagnostics-off";

    [DataField]
    public ProtoId<AlertPrototype> FansOKAlert = "IPCFansOk";
    [DataField]
    public ProtoId<AlertPrototype> FansOffAlert = "IPCFansOff";
    [DataField]
    public ProtoId<AlertPrototype> FansEfficiencyLowAlert = "IPCFansEfficiencyLow";
    [DataField]
    public ProtoId<AlertCategoryPrototype> AlertsCategory = "IPCCirculation";

    [DataField]
    public float FansEfficiencyLowThreshold = 0.5f;
    public ProtoId<AlertPrototype> CurrentAlert = "";

    [DataField]
    public TimeSpan RefreshRate = TimeSpan.FromSeconds(1);
    [ViewVariables]
    public TimeSpan NextUpdate;

    [AutoNetworkedField, ViewVariables]
    public FanMode? CurrentMode = null;
    [AutoNetworkedField, ViewVariables]
    public float CurrentEfficiency = 1;
    [AutoNetworkedField, ViewVariables]
    public float CurrentTemp = 0;

    public List<FanMode> OrderedFanModes => [.. FanModes.OrderByDescending(p => p.MinTemp)];
}

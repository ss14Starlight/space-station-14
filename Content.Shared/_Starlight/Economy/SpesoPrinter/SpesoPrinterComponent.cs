using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Economy.SpesoPrinter;

/// <summary>
/// A Component that makes machine print currency(actually any EntProto), 
/// additionaly ramping up in speed and output but also increasing
/// power consumption and heat generation over time
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SpesoPrinterComponent : Component
{
    /// <summary>
    /// prototype to print
    /// </summary>
    [DataField]
    public EntProtoId PrintedEntity = "NTCredit100";

    /// <summary>
    /// Current print level, ioncreases with each print, affecting speed, output, power and heat
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PrintLevel = 0;

    /// <summary>
    /// Maximum print "level" machine can achieve
    /// </summary>
    [DataField]
    public int MaxPrintLevel = 5;

    /// <summary>
    /// Base interval between prints in seconds
    /// </summary>
    [DataField]
    public float BasePrintInterval = 900f; // 15min

    /// <summary>
    /// How much the interval decreases per level, lower = faster
    /// </summary>
    [DataField]
    public float IntervalDecreasePerLevel = 0.8f;

    /// <summary>
    /// Minimum print interval in seconds
    /// </summary>
    [DataField]
    public float MinPrintInterval = 300f; // 5min

    [DataField]
    public int BaseCreditsPerPrint = 100;

    [DataField]
    public int CreditsIncreasePerLevel = 20;

    [DataField]
    public float BasePowerDraw = 5000f; // 5kW

    /// <summary>
    /// Power draw multiplier per level
    /// </summary>
    [DataField]
    public float PowerIncreasePerLevel = 1.5f;

    /// <summary>
    /// Base heat generated per print
    /// </summary>
    [DataField]
    public float BaseHeatPerPrint = 5000f;

    /// <summary>
    /// Heat multiplier per level
    /// </summary>
    [DataField]
    public float HeatIncreasePerLevel = 1.3f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPrintTime = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;
    [DataField, AutoNetworkedField]
    public bool Printing = false;
    [DataField]
    public bool WasPowered = false;
    
    [DataField]
    public Vector2 SpawnOffset = new(0f, -0.5f);

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg")
    {
        Params = AudioParams.Default.WithVolume(-4f)
    };

    [Serializable, NetSerializable]
    public enum SpesoPrinterVisuals : byte
    {
        Powered,
        Printing
    }

    [Serializable, NetSerializable]
    public enum SpesoPrinterLayers : byte
    {
        Base,
        Unlit
    }

}


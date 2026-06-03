using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
namespace Content.Shared.Starlight.Medical.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryToolComponent : Component
{
    [DataField]
    public SurgeryToolBehavior Behavior = new();
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class MultipleSurgeryToolComponent : Component
{
    /// <summary>
    /// List of behaviors, where key is type of component(cutted) like: "Tool" or "BoneGel".
    /// </summary>
    [DataField]
    public Dictionary<string, SurgeryToolBehavior> Behaviors = new();
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem), typeof(SharedBodyScannerSystem))]
[AutoGenerateComponentState]
public sealed partial class OperatingTableComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Scanner;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedBodyScannerSystem))]
[AutoGenerateComponentState]
public sealed partial class BodyScannerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? TableEntity;

    /// <summary>
    /// The machine linking port
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> LinkingPort = "BodyScannerSender";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class BoneGelComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "bone gel";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class BoneSawComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a bone saw";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class BoneSetterComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a bone setter";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class CauteryComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a cautery";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class HemostatComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a hemostat";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class RetractorComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a retractor";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class ScalpelComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a scalpel";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgicalDrillComponent : Component, ISurgeryToolComponent
{
    public string ToolName => "a surgical drill";
}

[DataDefinition]
public sealed partial class SurgeryToolBehavior
{
    /// <summary>
    /// Determines how fast you will do operation. For example if operation takes 5s, and you have speed = 2, it will take 2.5 sec.
    /// </summary>
    [DataField]
    public float Speed = 1;

    /// <summary>
    /// The success rate for the operation, represented as a value between 0 and 1.
    /// </summary>
    /// <remarks>The success rate determines the probability that the associated operation will succeed. A
    /// value of 1 indicates a guaranteed success, while a value of 0 indicates certain failure. Values outside the
    /// range of 0 to 1 may result in undefined behavior.</remarks>
    [DataField]
    public float SuccessRate = 1f;

    /// <summary>
    /// Determines if this surgery tool will make it so you bypass chances check.
    /// </summary>
    [DataField]
    public bool AlwaysSuccess = false;

    /// <summary>
    /// The sound to be played when the operation starts.
    /// </summary>
    [DataField]
    public SoundSpecifier? StartSound;

    /// <summary>
    /// The sound to be played when the operation ends.
    /// </summary>
    [DataField]
    public SoundSpecifier? EndSound;

    /// <summary>
    /// Container from which we will get reagent. (If it's required for step)
    /// </summary>
    [DataField]
    public string? ReagentContainer = "container";
}

using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
namespace Content.Shared._Starlight.Medical.Surgery.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryToolComponent : Component
{
    /*
    [DataField, AutoNetworkedField]
    public float Speed = 1;

    [DataField, AutoNetworkedField]
    public float SuccessRate = 1f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? StartSound;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? EndSound;*/
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
    public LocId ToolName => "surgery-tool-name-bone-gel";

    //FarHorizons Start
    public string ToolType => "BoneGel";

    [DataField]
    public float Speed { get; private set; } = 1;

    [DataField]
    public float SuccessRate { get; private set; } = 1f;

    [DataField]
    public SoundSpecifier? StartSound { get; private set; }

    [DataField]
    public SoundSpecifier? EndSound { get; private set; }
    //FarHorizons End
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class BoneSawComponent : Component, ISurgeryToolComponent
{
    public LocId ToolName => "surgery-tool-name-bone-saw";

    //FarHorizons Start
    public string ToolType => "BoneSaw";

    [DataField]
    public float Speed { get; private set; } = 1;

    [DataField]
    public float SuccessRate { get; private set; } = 1f;

    [DataField]
    public SoundSpecifier? StartSound { get; private set; }

    [DataField]
    public SoundSpecifier? EndSound { get; private set; }
    //FarHorizons End
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class BoneSetterComponent : Component, ISurgeryToolComponent
{
    public LocId ToolName => "surgery-tool-name-bone-setter";

    //FarHorizons Start
    public string ToolType => "BoneSetter";

    [DataField]
    public float Speed { get; private set; } = 1;

    [DataField]
    public float SuccessRate { get; private set; } = 1f;

    [DataField]
    public SoundSpecifier? StartSound { get; private set; }

    [DataField]
    public SoundSpecifier? EndSound { get; private set; }
    //FarHorizons End
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class CauteryComponent : Component, ISurgeryToolComponent
{
    public LocId ToolName => "surgery-tool-name-cautery";

    //FarHorizons Start
    public string ToolType => "Cautery";

    [DataField]
    public float Speed { get; private set; } = 1;

    [DataField]
    public float SuccessRate { get; private set; } = 1f;

    [DataField]
    public SoundSpecifier? StartSound { get; private set; }

    [DataField]
    public SoundSpecifier? EndSound { get; private set; }
    //FarHorizons End
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class HemostatComponent : Component, ISurgeryToolComponent
{
    public LocId ToolName => "surgery-tool-name-hemostat";

    //FarHorizons Start
    public string ToolType => "Hemostat";

    [DataField]
    public float Speed { get; private set; } = 1;

    [DataField]
    public float SuccessRate { get; private set; } = 1f;

    [DataField]
    public SoundSpecifier? StartSound { get; private set; }

    [DataField]
    public SoundSpecifier? EndSound { get; private set; }
    //FarHorizons End
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class RetractorComponent : Component, ISurgeryToolComponent
{
    public LocId ToolName => "surgery-tool-name-retractor";

    //FarHorizons Start
    public string ToolType => "Retractor";

    [DataField]
    public float Speed { get; private set; } = 1;

    [DataField]
    public float SuccessRate { get; private set; } = 1f;

    [DataField]
    public SoundSpecifier? StartSound { get; private set; }

    [DataField]
    public SoundSpecifier? EndSound { get; private set; }
    //FarHorizons End
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class ScalpelComponent : Component, ISurgeryToolComponent
{
    public LocId ToolName => "surgery-tool-name-scalpel";

    //FarHorizons Start
    public string ToolType => "Scalpel";

    [DataField]
    public float Speed { get; private set; } = 1;

    [DataField]
    public float SuccessRate { get; private set; } = 1f;

    [DataField]
    public SoundSpecifier? StartSound { get; private set; }

    [DataField]
    public SoundSpecifier? EndSound { get; private set; }
    //FarHorizons End
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgicalDrillComponent : Component, ISurgeryToolComponent
{
    public LocId ToolName => "surgery-tool-name-surgical-drill";

    //FarHorizons Start
    public string ToolType => "SurgicalDrill";

    [DataField]
    public float Speed { get; private set; } = 1;

    [DataField]
    public float SuccessRate { get; private set; } = 1f;

    [DataField]
    public SoundSpecifier? StartSound { get; private set; }

    [DataField]
    public SoundSpecifier? EndSound { get; private set; }
    //FarHorizons End

    //FarHorizons Start
    [RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
    public sealed partial class CrowbarSurgeryComponent : Component, ISurgeryToolComponent
    {
        public LocId ToolName => "surgery-tool-name-crowbar";
        public string ToolType => "CrowbarSurgery";

        [DataField]
        public float Speed { get; private set; } = 1;

        [DataField]
        public float SuccessRate { get; private set; } = 1f;

        [DataField]
        public SoundSpecifier? StartSound { get; private set; }

        [DataField]
        public SoundSpecifier? EndSound { get; private set; }
    }

    [RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
    public sealed partial class MultitoolSurgeryComponent : Component, ISurgeryToolComponent
    {
        public LocId ToolName => "surgery-tool-name-multitool";
        public string ToolType => "MultitoolSurgery";

        [DataField]
        public float Speed { get; private set; } = 1;

        [DataField]
        public float SuccessRate { get; private set; } = 1f;

        [DataField]
        public SoundSpecifier? StartSound { get; private set; }

        [DataField]
        public SoundSpecifier? EndSound { get; private set; }
    }

    [RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
    public sealed partial class ScrewdriverSurgeryComponent : Component, ISurgeryToolComponent
    {
        public LocId ToolName => "surgery-tool-name-screwdriver";
        public string ToolType => "ScrewdriverSurgery";

        [DataField]
        public float Speed { get; private set; } = 1;

        [DataField]
        public float SuccessRate { get; private set; } = 1f;

        [DataField]
        public SoundSpecifier? StartSound { get; private set; }

        [DataField]
        public SoundSpecifier? EndSound { get; private set; }
    }

    [RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
    public sealed partial class WelderSurgeryComponent : Component, ISurgeryToolComponent
    {
        public LocId ToolName => "surgery-tool-name-welder";
        public string ToolType => "WelderSurgery";

        [DataField]
        public float Speed { get; private set; } = 1;

        [DataField]
        public float SuccessRate { get; private set; } = 1f;

        [DataField]
        public SoundSpecifier? StartSound { get; private set; }

        [DataField]
        public SoundSpecifier? EndSound { get; private set; }
    }

    [RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
    public sealed partial class WirecutterSurgeryComponent : Component, ISurgeryToolComponent
    {
        public LocId ToolName => "surgery-tool-name-wirecutter";
        public string ToolType => "WirecutterSurgery";

        [DataField]
        public float Speed { get; private set; } = 1;

        [DataField]
        public float SuccessRate { get; private set; } = 1f;

        [DataField]
        public SoundSpecifier? StartSound { get; private set; }

        [DataField]
        public SoundSpecifier? EndSound { get; private set; }
    }

    [RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
    public sealed partial class WrenchSurgeryComponent : Component, ISurgeryToolComponent
    {
        public LocId ToolName => "surgery-tool-name-wrench";
        public string ToolType => "WrenchSurgery";

        [DataField]
        public float Speed { get; private set; } = 1;

        [DataField]
        public float SuccessRate { get; private set; } = 1f;

        [DataField]
        public SoundSpecifier? StartSound { get; private set; }

        [DataField]
        public SoundSpecifier? EndSound { get; private set; }
    }
}
//FarHorizons End

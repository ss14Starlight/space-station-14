using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
namespace Content.Shared._Starlight.Medical.Surgery.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class EyeImplantComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class NoseImplantComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class HandImplantComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class BrainImplantComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganBrainComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganAppendixComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganEarsComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganLungsComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganHeartComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganStomachComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganLiverComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganKidneysComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class LeftArmComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class RightArmComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganShellComponent : Component;
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganTongueComponent : Component
{
    [DataField]
    public bool IsMuted;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganEyesComponent : Component
{
    [DataField]
    public int? EyeDamage;
    [DataField]
    public int? MinDamage;
}
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganVisualizationComponent : Component
{
    [DataField]
    public HumanoidVisualLayers Layer;
    [DataField]
    public Dictionary<string, ProtoId<HumanoidSpeciesSpriteLayer>?> Prototypes = new() { { "Default", null } };
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class FunctionalOrganComponent : Component
{
    [DataField]
    public bool IsCybernetic = true;

    [DataField("comps")]
    public ComponentRegistry? Components;

    // Populated at install time with the component types this specific organ instance actually
    // added, so extraction only removes what it installed, not whatever's currently present.
    public HashSet<Type> Installed = [];
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class TaggedOrganComponent : Component
{
    [DataField]
    public List<ProtoId<TagPrototype>> AddTags = new();

    [DataField]
    public List<ProtoId<TagPrototype>> RemoveTags = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StorageOrganComponent : Component
{
    [DataField]
    public EntProtoId? OrganAction { get; set; }

    /// <summary>
    /// The action entity of the storage organ.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public string ActionKey;
}

/// <summary>
/// Used for opening the storage organ via action.
/// </summary>
public sealed partial class OpenStorageOrganEvent : InstantActionEvent
{
    [DataField]
    public string Key = "InternalStorage";
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MarkingOrganComponent : Component
{
    [DataField]
    public List<ProtoId<MarkingPrototype>> AppliedMarkings = [];

    [DataField]
    public Dictionary<ProtoId<MarkingPrototype>, (bool isGlowing, IReadOnlyList<Color> markingColors)> Markings = [];

    [DataField]
    public bool StoreMarkings = false;

    [DataField]
    public bool IsGlowing = false;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DamageModifierOrganComponent : Component
{
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganDamageComponent : Component
{
    [DataField]
    public DamageSpecifier? Damage;
}

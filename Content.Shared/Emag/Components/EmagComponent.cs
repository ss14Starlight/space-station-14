using Content.Shared.Emag.Systems;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization;
using Content.Shared.Silicons.Laws;
using Content.Shared.Radio; //#Starlight
using Content.Shared.NPC.Prototypes; // Starlight
using Content.Shared.Access; // Starlight

namespace Content.Shared.Emag.Components;

[Access(typeof(EmagSystem))]
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class EmagComponent : Component
{
    /// <summary>
    /// The tag that marks an entity as immune to emags
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public ProtoId<TagPrototype> EmagImmuneTag = "EmagImmune";

    /// <summary>
    /// What type of emag effect this device will do
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public EmagType EmagType = EmagType.Interaction;

    /// <summary>
    /// What sound should the emag play when used
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public SoundSpecifier EmagSound = new SoundCollectionSpecifier("sparks");

    //#region Starlight
    /// <summary>
    /// The faction this emag belongs to. Typically, either syndicate or nanotrasen.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public ProtoId<NpcFactionPrototype>? OwningFaction = "Syndicate"; /// Starlight edit
    
    /// <summary>
    /// The access group to grant to electronics that get emagged
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public List<ProtoId<AccessGroupPrototype>> AccessGroups = [];
    
    /// <summary>
    /// should this emag also destroy the transponder
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool DestroyTransponder = false;

    /// <summary>
    /// What lawset should borgs get when emagged. note. fully replaces the lawset and prevents the "only x and those they designate are crew" and secrecy laws.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public ProtoId<SiliconLawsetPrototype>? Lawset = null;

    /// <summary>
    /// What components should be added to a borg chassis when emagged
    /// </summary>
    [DataField]
    public ComponentRegistry? Components = null;

    /// <summary>
    /// What radio channels should be added to a emagged borg chassis
    /// </summary>
    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> ChannelAdd = ["Syndicate"];

    /// <summary>
    /// Overrides borg emagged sound if specified.
    /// </summary>
    [DataField] public SoundSpecifier? EmaggedSoundOverride;
    
    /// <summary>
    /// Whether to even play the emagged sound or not.
    /// </summary>
    [DataField] public bool DoEmaggedSound = true;
    //#endregion Starlight
}

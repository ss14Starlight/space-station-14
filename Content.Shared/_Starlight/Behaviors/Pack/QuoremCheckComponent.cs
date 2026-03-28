using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Starlight.Behaviors.Pack;

/// <summary>
/// Used for mobs that exhibit pack behavior. Entities with this component
/// are able to join packs. Members of a pack will defend each other if attacked,
/// and can become hostile upon reaching a specific member count.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class QuoremCheckComponent : Component 
{
    /// <summary>
    /// The next time recruiting into the pack will be attempted.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextRecruitAttempt;

    /// <summary>
    /// Minimum length between each recruit attempt.
    /// </summary>
    [DataField] public TimeSpan MinRecruitAttemptInterval = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Maximum length between each recruit attempt.
    /// </summary>
    [DataField] public TimeSpan MaxRecruitAttemptInterval = TimeSpan.FromSeconds(6);

    /// <summary>
    /// The recruit radius, which defines how close other entities have to be to attempt to recruit them.
    /// </summary>
    [DataField] public float RecruitRadius = 5.0f;
    
    /// <summary>
    /// The quorem threshold, at which point the pack will be considered sufficiently large.
    /// </summary>
    [DataField] public int QuoremThreshold = 3;

    [DataField(required: true)] public string PackTag;

    [DataField(readOnly: true)] public bool IsHostile;

    [DataField] public int PackId;
    
    [DataField] public ProtoId<NpcFactionPrototype> QuoremFaction = "Xeno";
    
    [DataField] public ProtoId<NpcFactionPrototype> DefaultFaction = "Passive";

    [DataField] public ProtoId<EntityPrototype>? QuoremEffect;
    
    [DataField] public SoundSpecifier? QuoremSound;
}

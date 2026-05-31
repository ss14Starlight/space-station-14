using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Spider;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSpiderSystem))]
public sealed partial class SpiderComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("webPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string WebPrototype = "SpiderWeb";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("webAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string WebAction = "ActionSpiderWeb";

    [DataField] public EntityUid? Action;

    /// <summary>
    /// Whether the spider will spawn webs when not controlled by a player.
    /// </summary>
    [DataField]
    public bool SpawnsWebsAsNonPlayer = true;

    /// <summary>
    /// The cooldown in seconds between web spawns when not controlled by a player.
    /// </summary>
    [DataField]
    public TimeSpan WebSpawnCooldown = TimeSpan.FromSeconds(45f);

    /// <summary>
    /// The next time the spider can spawn a web when not controlled by a player.
    /// </summary>
    [DataField]
    public TimeSpan? NextWebSpawn;

    #region Starlight

    /// <summary>
    /// Determines should we spawn only 1 web or five(like plus symbol)?
    /// </summary>
    [DataField]
    public bool OneWebSpawn = true;

    /// <summary>
    /// Determines ability of breaking webs. Added to avoid evolution farm.
    /// </summary>
    [DataField]
    public bool CantBreakWeb = true;

    /// <summary>
    /// Is this spider has building?
    /// </summary>
    [DataField]
    public bool HasBuilding = false;

    /// <summary>
    /// Building action proto.
    /// </summary>
    [DataField]
    public string BuildingActionProto = "";

    public EntityUid? BuildingAction;

    /// <summary>
    /// Determines can we replace web if there's another web on this tile?
    /// </summary>
    [DataField]
    public bool ReplacementAllowed = true;

    #endregion
}

public sealed partial class SpiderWebActionEvent : InstantActionEvent { }

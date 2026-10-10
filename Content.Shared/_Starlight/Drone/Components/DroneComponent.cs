using Content.Shared.Item;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Drone.Components;

/// <summary>
/// Component for drones. Pretty self explanatory huh?
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DroneComponent : Component
{
    /// <summary>
    /// List of tags that drones can interact with.
    /// If empty, all non-blacklisted items are allowed.
    /// </summary>
    [DataField]
    public HashSet<string> InteractionWhitelist = [];

    /// <summary>
    /// List of tags that drones cannot interact with.
    /// Takes priority over whitelist.
    /// </summary>
    [DataField]
    public HashSet<string> InteractionBlacklist = [];

    /// <summary>
    /// Largest item size a drone can pick up. Anything bigger is refused.
    /// </summary>
    [DataField]
    public ProtoId<ItemSizePrototype> MaxItemSize = "Normal";

    /// <summary>
    /// Whether the drone currently has power.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = true;

    /// <summary>
    /// Speed multiplier applied while the drone has no power.
    /// </summary>
    [DataField]
    public float UnpoweredSpeedModifier = 0.4f;

    /// <summary>
    /// Actions that keep working while unpowered. Everything else is disabled.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> PowerlessActions = ["ActionViewLaws"];
}

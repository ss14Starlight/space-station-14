namespace Content.Shared.Drone;

/// <summary>
/// Component for drones. Pretty self explanatory huh?
/// </summary>
[RegisterComponent]
public sealed partial class DroneComponent : Component
{
    /// <summary>
    /// List of tags that drones can interact with.
    /// If empty, all non-blacklisted items are allowed.
    /// </summary>
    [DataField]
    public HashSet<string> InteractionWhitelist = new()
    {
        // Todo
    };

    /// <summary>
    /// List of tags that drones cannot interact with.
    /// Takes priority over whitelist.
    /// </summary>
    [DataField]
    public HashSet<string> InteractionBlacklist = new()
    {
        // Todo
    };
}
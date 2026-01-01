using Robust.Shared.GameStates;

namespace Content.Shared.Follower.Components;

[RegisterComponent]
[Access(typeof(FollowerSystem))]
[NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FollowerComponent : Component
{
    [AutoNetworkedField, DataField("following")]
    public EntityUid Following;

    // Starlight-start
    /// <summary>
    /// If true the follower orbits/haunts the target; otherwise stays fixed.
    /// </summary>
    [AutoNetworkedField, DataField("orbit")]
    public bool Orbit = true;
    // Starlight-end
}

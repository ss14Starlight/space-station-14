using Robust.Shared.GameStates;

namespace Content.Shared.Spider;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSpiderSystem))]
public sealed partial class SpiderWebObjectComponent : Component
{
    /// <summary>
    /// Whether a spider spawning webs can replace this. Can be set to false for web structures.
    /// </summary>
    [DataField] public bool Replaceable = true;
}

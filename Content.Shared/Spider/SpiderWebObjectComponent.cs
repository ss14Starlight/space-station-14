using Robust.Shared.GameStates;

namespace Content.Shared.Spider;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSpiderSystem))]
public sealed partial class SpiderWebObjectComponent : Component
{
    /// <summary>
    /// Whether a spider spawning webs can overwrite this. Plain webs are; structures like cocoons are not.
    /// </summary>
    [DataField]
    public bool Replaceable = true;
}

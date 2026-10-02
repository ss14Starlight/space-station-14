using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.OnCollide;
[RegisterComponent]
public sealed partial class SpawnOnCollideComponent : Component
{
    /// <summary>
    /// Entity prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype = string.Empty;
    [DataField]
    public bool Collided;
}

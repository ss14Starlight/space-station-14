using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Damage.Components;

public sealed partial class DamagedByContactComponent
{
    [ViewVariables]
    public bool DamageDead;
}

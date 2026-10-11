using Content.Shared.Kitchen.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Kitchen.Components;

public sealed partial class ReagentGrinderComponent : Component
{
    [DataField]
    public bool NeedsPower = true;
}

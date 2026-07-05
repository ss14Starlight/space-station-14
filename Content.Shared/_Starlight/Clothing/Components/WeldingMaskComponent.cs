using Content.Shared._Starlight.Clothing.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Clothing.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(WeldingMaskSystem))]
public sealed partial class WeldingMaskComponent : Component
{
}

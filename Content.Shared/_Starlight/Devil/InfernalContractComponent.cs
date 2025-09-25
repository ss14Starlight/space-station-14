using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent]
public sealed partial class InfernalContractComponent : Component
{
    EntityUid Author;

    bool Completed = false;

    EntityUid? Signator = null;
}
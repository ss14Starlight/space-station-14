using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CosmicEffigyComponent : Component
{
    [DataField]
    public EntityUid? Colossus;
}

public sealed class CosmicEffigyDestroyedEvent : EntityEventArgs
{
}

using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CosmicEffigyComponent : Component
{
    public EntityUid? Colossus;
}

public sealed class CosmicEffigyDestroyedEvent : EntityEventArgs
{
}

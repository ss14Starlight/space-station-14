using Content.Shared.DoAfter;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CosmicLambdaParticleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class CosmicLambdaParticleSourceComponent : Component
{
    public DoAfterId? DoAfterId = null;
}

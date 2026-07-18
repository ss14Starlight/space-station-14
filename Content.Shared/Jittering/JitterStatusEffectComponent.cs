using Robust.Shared.GameStates;

namespace Content.Shared.Jittering;

/// <summary>
/// Marker on the <c>StatusEffectJitter</c> entity. Bridges to <see cref="JitteringComponent"/> on the target.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class JitterStatusEffectComponent : Component;

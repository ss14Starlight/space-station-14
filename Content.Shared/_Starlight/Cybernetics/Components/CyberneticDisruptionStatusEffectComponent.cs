using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Cybernetics.Components;

/// <summary>
/// Cybernetic Disruption as a status effect.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedCyberneticDisruptionSystem))]
public sealed partial class CyberneticDisruptionStatusEffectComponent : Component;

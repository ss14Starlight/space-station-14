using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Cybernetics.Components;

/// <summary>
/// This is used to temporarily prevent an entity from using certain cyberware.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedCyberneticDisruptionSystem))]
public sealed partial class CyberneticDisruptionComponent : Component;

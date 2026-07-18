using Robust.Shared.GameStates;

namespace Content.Shared.Flash.Components;

/// <summary>
/// Marker on the <c>StatusEffectFlashed</c> entity. Adds a shader on the client that obstructs vision.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlashedStatusEffectComponent : Component;

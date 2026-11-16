using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Reptilian.Components;

/// <summary>
/// Injectors and injecting weapons with this get through <see cref="ThickScalesComponent"/> as if it wasn't there.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PiercesThickScalesComponent : Component;

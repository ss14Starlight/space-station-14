using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Chemistry.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SLSolutionRegenerationSystem))]
public sealed partial class SLActiveSolutionRegenerationComponent : Component;

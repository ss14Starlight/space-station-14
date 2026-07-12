namespace Content.Shared._Starlight.Defects.Components;

// Marker component that causes DefectSystem to roll each
// DefectComponent.Prob at MapInit, removing defects that fail.
[RegisterComponent]
public sealed partial class RandomDefectsComponent : Component
{
}

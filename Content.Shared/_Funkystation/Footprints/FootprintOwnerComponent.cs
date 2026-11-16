using Content.Shared.Chemistry.Components;

namespace Content.Shared._Funkystation.Footprints;

[RegisterComponent]
public sealed partial class FootprintOwnerComponent : Component
{

    [DataField] public float MaxFootVolume = 10f;
    [DataField] public float MaxBodyVolume = 20f;

    [DataField] public float MinFootprintVolume = 0.5f; // Starlight, renamed this to be consistent
    [DataField] public float MaxFootprintVolume = 0.75f; // Starlight, changed to 0.75

    [DataField] public float MinBodyprintVolume = 2f; // Starlight, this one too
    [DataField] public float MaxBodyprintVolume = 5f;

    [DataField] public float FootstepDistance = 0.5f;
    [DataField] public float DragDistance = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DistanceWalked;

    [DataField] public float AlternateStepOffset = 0.0625f;

    /// <summary>
    /// Cached solution entity containing residue currently carried by this entity.
    /// </summary>
    [ViewVariables]
    public Entity<SolutionComponent>? Solution;
}

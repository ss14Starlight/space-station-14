namespace Content.Shared._Funkystation.Footprints;

[RegisterComponent]
public sealed partial class FootprintOwnerComponent : Component
{
    // Starlight, reducing these more or less by a factor of 10 so we don't eat the entirety of a puddle in one go.
    [DataField] public float MaxFootVolume = 1f; // Starlight
    [DataField] public float MaxBodyVolume = 2f; // Starlight

    [DataField] public float MinPrintVolume = 0.05f; // Starlight
    [DataField] public float MaxFootprintVolume = 0.1f; // Starlight

    [DataField] public float MinBodyPrintVolume = 0.2f; // Starlight
    [DataField] public float MaxBodyprintVolume = 0.5f; // Starlight

    [DataField] public float FootstepDistance = 0.5f;
    [DataField] public float DragDistance = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DistanceWalked;

    [DataField] public float AlternateStepOffset = 0.0625f;
}

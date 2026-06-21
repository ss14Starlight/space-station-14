namespace Content.Shared._Funkystation.Footprints;

[RegisterComponent]
public sealed partial class FootprintOwnerComponent : Component
{
    [DataField] public float MaxFootVolume = 10f;
    [DataField] public float MaxBodyVolume = 20f;

    [DataField] public float MinPrintVolume = 0.5f;
    [DataField] public float MaxFootprintVolume = 1f;

    [DataField] public float MinBodyPrintVolume = 2f;
    [DataField] public float MaxBodyprintVolume = 5f;

    [DataField] public float FootstepDistance = 0.5f;
    [DataField] public float DragDistance = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DistanceWalked;

    [DataField] public float AlternateStepOffset = 0.0625f;
}

namespace Content.Shared._Funkystation.Footprints;

[RegisterComponent]
public sealed partial class FootprintOwnerComponent : Component
{
    // Moff start - Divide all these values by 10. We dont want them messing with the puddle volume too much.
    // Of course, we can change this if the specific volumes become important, but for now, they're not.
    [DataField] public float MaxFootVolume = 1f;
    [DataField] public float MaxBodyVolume = 2f;

    [DataField] public float MinPrintVolume = 0.05f;
    [DataField] public float MaxFootprintVolume = 0.1f;

    [DataField] public float MinBodyPrintVolume = 0.2f;
    [DataField] public float MaxBodyprintVolume = 0.5f;
    // Moff end

    [DataField] public float PrintMixAmount = 0.1f; // Moff - When the print volume is full, how much should other puddles be mixed when theyre stepped in.

    [DataField] public float FootstepDistance = 0.5f;
    [DataField] public float DragDistance = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DistanceWalked;

    [DataField] public float AlternateStepOffset = 0.0625f;
}

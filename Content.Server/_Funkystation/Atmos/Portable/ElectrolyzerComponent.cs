using Robust.Shared.Audio;

namespace Content.Server._Funkystation.Atmos.Portable;

[RegisterComponent]
public sealed partial class ElectrolyzerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CurrentFuel { get; set; } = 0f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PlasmaFuelConversion { get; set; } = 200000f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float UraniumFuelConversion { get; set; } = 1000000f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsPowered { get; set; } = false;

    [DataField("onSound")]
    public SoundSpecifier? OnSound;
}

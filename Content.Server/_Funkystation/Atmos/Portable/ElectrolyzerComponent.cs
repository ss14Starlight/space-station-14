using Robust.Shared.Audio;

namespace Content.Server._Funkystation.Atmos.Portable;

[RegisterComponent]
public sealed partial class ElectrolyzerComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)] /// Starlight
    public float Efficiency { get; set; } = 1f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CurrentFuel { get; set; } = 0f;

    [DataField, ViewVariables(VVAccess.ReadWrite)] /// Starlight: Changed value
    public float PlasmaFuelConversion { get; set; } = 400000f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsPowered { get; set; } = false;

    [DataField, ViewVariables(VVAccess.ReadWrite)] /// Starlight
    public bool Passive { get; set; } = false;

    [DataField("onSound")]
    public SoundSpecifier? OnSound;
}

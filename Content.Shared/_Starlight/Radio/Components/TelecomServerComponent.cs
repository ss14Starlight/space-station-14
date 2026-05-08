namespace Content.Shared.Radio.Components;

/// <summary>
/// Entities with <see cref="TelecomServerComponent"/> are needed to transmit messages using headsets.
/// They also need to be powered by <see cref="ApcPowerReceiverComponent"/>
/// have <see cref="EncryptionKeyHolderComponent"/> and filled with encryption keys
/// of channels in order for them to work on the same map as server.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomServerComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public bool Overheated;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool SpacedDisabled;
    
    /// <summary>Heat added to each surrounding tile per second while powered, in kJ.</summary>
    [DataField]
    public float HeatPerTilePerSecond = 300f;

    /// <summary>Surrounding gas temperature at which the server shuts itself off, in Kelvin.</summary>
    [DataField]
    public float OverheatTemperature = Atmospherics.FireMinimumTemperatureToExist + 25f;

    /// <summary>Surrounding gas temperature below which the overheated server may restart, in Kelvin.</summary>
    [DataField]
    public float CooldownTemperature = Atmospherics.T20C + 10f;
}

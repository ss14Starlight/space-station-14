using Content.Shared.Atmos;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Weapons.Ranged.Components;

/// <summary>
/// Every shot heats the gun up through the temperature system, and the air around it cools it back down.
/// A hot gun starts to jam, and an overheated one melts its firing pin.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class GunHeatComponent : Component
{
    /// <summary>
    /// Heat in joules a single shot adds to the gun. The temperature rise depends on the gun's heat capacity.
    /// </summary>
    [DataField]
    public float HeatPerShot = 750f;

    /// <summary>
    /// How well the surrounding air cools the gun.
    /// </summary>
    [DataField]
    public float CoolingEfficiency = 0.05f;

    /// <summary>
    /// Heat in watts per kelvin above room temperature that the gun loses on its own, even in vacuum.
    /// </summary>
    [DataField]
    public float PassiveCooling = 2.5f;

    /// <summary>
    /// Minimum temperature at which the gun starts to glow red-hot.
    /// </summary>
    [DataField]
    public float GlowTemperature = Atmospherics.T20C + 50f;

    /// <summary>
    /// The gun may jam from this temperature.
    /// </summary>
    [DataField]
    public float JamTemperature = Atmospherics.T20C + 160f;

    /// <summary>
    /// Chance to jam per shot once the gun reaches <see cref="MeltTemperature"/>; it grows linearly from <see cref="JamTemperature"/>.
    /// </summary>
    [DataField]
    public float MaxJamChance = 0.35f;

    /// <summary>
    /// The firing pin melts at this temperature.
    /// </summary>
    [DataField]
    public float MeltTemperature = Atmospherics.T20C + 360f;

    /// <summary>
    /// The entity prototype of the melted firing pin.
    /// </summary>
    [DataField]
    public EntProtoId? MeltedPin = "FiringPinMelted";

    /// <summary>
    /// Current gun temperature.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Temperature = Atmospherics.T20C;

    /// <summary>
    /// Whether the gun is currently jammed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Jammed;

    /// <summary>
    /// The sound that plays when the gun jams.
    /// </summary>
    [DataField]
    public SoundSpecifier JamSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");

    /// <summary>
    /// The sound that plays when the gun is unjammed.
    /// </summary>
    [DataField]
    public SoundSpecifier UnjamSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Cock/smg_cock.ogg");

    /// <summary>
    /// The sound that plays when the gun melts.
    /// </summary>
    [DataField]
    public SoundSpecifier MeltSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");

    [DataField]
    public TimeSpan PopupCooldown = TimeSpan.FromSeconds(1);
    public TimeSpan NextPopupTime;
}


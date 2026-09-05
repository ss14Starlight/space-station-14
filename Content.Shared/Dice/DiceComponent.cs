using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Dice;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedDiceSystem))]
[AutoGenerateComponentState(true)]
public sealed partial class DiceComponent : Component
{
    [DataField]
    public SoundSpecifier Sound { get; private set; } = new SoundCollectionSpecifier("Dice");

    /// <summary>
    ///     Multiplier for the value  of a die. Applied after the <see cref="Offset"/>.
    /// </summary>
    [DataField]
    public int Multiplier { get; private set; } = 1;

    /// <summary>
    ///     Quantity that is subtracted from the value of a die. Can be used to make dice that start at "0". Applied
    ///     before the <see cref="Multiplier"/>
    /// </summary>
    [DataField]
    public int Offset { get; private set; } = 0;

    // Starlight begin - No reason this shouldn't be networked automatically. Makes it easier to test things.
    [DataField, AutoNetworkedField]
    public int Sides { get; set; } = 20;
    // Starlight end

    /// <summary>
    ///     The currently displayed value.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public int CurrentValue { get; set; } = 20;

    #region Starlight

    /// Value to rig the outcome of the roll for more consistent testing.
    [DataField, AutoNetworkedField] public int? RiggedValue;

    #endregion

}

using Content.Server.Wires;
using Content.Shared._Starlight.Silicons.Bots;
using Content.Shared.Wires;

namespace Content.Server._Starlight.Silicons.Bots;

/// <summary>
/// One of a MULE's wires. Which one is set per wire in the layout.
/// </summary>
public sealed partial class MuleWireAction : ComponentWireAction<MuleComponent>
{
    /// <summary>
    /// Which of the MULE's wires this is.
    /// </summary>
    [DataField(required: true)]
    public MuleWires Wire;

    private MuleSystem _mule = default!;

    public override string Name
    {
        get => $"wire-name-mule-{Wire.ToString().ToLowerInvariant()}";
        set { }
    }

    public override Color Color { get; set; } = Color.Orange;

    public override object? StatusKey => Wire;

    public override void Initialize()
    {
        base.Initialize();
        _mule = EntityManager.System<MuleSystem>();
    }

    /// <summary>
    /// Several of these sit in one layout, one per wire.
    /// </summary>
    public override bool AddWire(Wire wire, int count) => true;

    public override StatusLightState? GetLightState(Wire wire, MuleComponent component)
    {
        if (Wire is not (MuleWires.Power1 or MuleWires.Power2))
            return null;

        return _mule.HasPower((wire.Owner, component)) ? StatusLightState.On : StatusLightState.Off;
    }

    public override bool Cut(EntityUid user, Wire wire, MuleComponent component)
    {
        _mule.SetWireCut((wire.Owner, component), Wire, true, user);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, MuleComponent component)
    {
        _mule.SetWireCut((wire.Owner, component), Wire, false, user);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, MuleComponent component)
    {
        _mule.PulseWire((wire.Owner, component), Wire);
    }
}

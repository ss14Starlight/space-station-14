using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping
{
    [Serializable, NetSerializable]
    public enum OutletInjectorVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum PassiveVentVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum VentScrubberVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum PumpVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum FilterVisuals : byte
    {
        Enabled,
        Powered,

        Core,
        Inlet,
        Outlet,
        Side,
    }

    /// <summary>
    /// The visuals state of *one port* of a mixer or filter.
    /// </summary>
    [Serializable, NetSerializable]
    public enum FilterPortVisualsState : byte
    {
        Off,
        SolidOrange, // Orange
        SolidGreen, // Solid green
        SolidYellow, // Solid yellow. Blocked but still nominal, e.g. mixer output is full.
        BlinkingYellow, // Blinking yellow. Blocked and requires attention
        SolidRed, // Solid red
        BlinkingRed, // Blinking red
        BlinkingRedOrange, // Blinking red/orange
    }

    [Serializable, NetSerializable]
    public enum PressureRegulatorVisuals : byte
    {
        State,
    }
}

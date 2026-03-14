using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.TicketMachine;

[Serializable, NetSerializable]
public enum TicketMachineVisuals
{
    isPowered, // Whether the ticket machine is powered
    isFilled, // Whether the ticket machine has paper
    Paper, // How much paper is left
    DisplayNumber // The number currently displayed
}

public enum TicketMachineVisualLayers : byte
{
    Paper,
    Display1, // For numbers 0-9
    Display2, // For numbers 10-99
    Display3, // For numbers 100-999
}

[Serializable, NetSerializable]
public enum TicketVisuals
{
    Number // Ticket number
}

public enum TicketVisualLayers : byte
{
    Number1, // For numbers 0-9
    Number2, // For numbers 10-99
    Number3 // For numbers 100-999
}
using Content.Shared.CartridgeLoader.Cartridges;

namespace Content.Server._Starlight.Tidr;

/// <summary>
///     Station-wide shared task board for Tidr (the NanoTask rework).
///     Lives on the station entity. Every NanoTask/Tidr cartridge on the
///     station reads from and writes to this one list, so all crew see the
///     same board instead of a private per-PDA notepad.
/// </summary>
[RegisterComponent]
public sealed partial class TidrBoardComponent : Component
{
    /// <summary>
    ///     The shared list of tasks for the whole station.
    /// </summary>
    [DataField]
    public List<NanoTaskItemAndId> Tasks = new();

    /// <summary>
    ///     Counter for generating unique task IDs across the station.
    /// </summary>
    [DataField]
    public int Counter = 1;
}

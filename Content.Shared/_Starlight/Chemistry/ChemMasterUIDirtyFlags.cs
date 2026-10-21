using Content.Server.Chemistry.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Chemistry;

/// <summary>
/// Flags for partial update of ChemMaster UI, each bit represents a data source could have changed.
/// </summary>
[Flags]
public enum ChemMasterUIDirtyFlags
{
    None = 0,

    /// <summary>
    /// All networked "settings" for the machine stored in <see cref="ChemMasterComponent"/>
    /// </summary>
    Settings = 1 << 0,

    /// <summary>
    /// Items added to or removed from storage in the machine.
    /// </summary>
    Inventory = 1 << 1,

    /// <summary>
    /// The buffer solution, and solutions in inserted beakers.
    /// </summary>
    Solutions = 1 << 2,

    /// <summary>
    /// The state of the plumbing output valve
    /// </summary>
    PlumbingOutput = 1 << 3,

    All = ~0,
}

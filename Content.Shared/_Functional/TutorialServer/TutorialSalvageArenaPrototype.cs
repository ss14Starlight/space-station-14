using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Builds a salvage bay grid plus a nearby debris grid for magnet/EVA/haul tutorials.
/// </summary>
[Prototype] //Tutorial: drop redundant type (RA0042)
public sealed partial class TutorialSalvageArenaPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Map-space offset of the debris grid from the bay spawn.
    /// </summary>
    [DataField]
    public Vector2 DebrisOffset = new(18f, 0f);
}
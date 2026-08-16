using System.Numerics;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Construction;

/// <summary>
/// A saved set of construction ghosts, stored relative to the tile centre of the ghost closest to the bottom left of the set.
/// </summary>
[DataDefinition]
public sealed partial class ConstructionTemplate
{
    /// <summary>
    /// The only template format version currently supported.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Version of the serialized template format.
    /// </summary>
    [DataField(required: true)]
    public int Version = CurrentVersion;

    /// <summary>
    /// Name of the map the ghosts were saved on.
    /// </summary>
    [DataField]
    public string MapName = string.Empty;

    /// <summary>
    /// Whether the ghosts were saved on a grid rather than directly on the map.
    /// </summary>
    [DataField]
    public bool OnGrid;

    /// <summary>
    /// Position of the origin within the saved map or grid.
    /// </summary>
    [DataField]
    public Vector2 Origin;

    /// <summary>
    /// Construction ghosts in this template, relative to <see cref="Origin"/>.
    /// </summary>
    [DataField]
    public List<ConstructionTemplateEntry> Entries = new();
}

/// <summary>
/// A construction ghost saved in a <see cref="ConstructionTemplate"/>.
/// </summary>
[DataDefinition]
public sealed partial class ConstructionTemplateEntry
{
    /// <summary>
    /// Construction recipe used to create the ghost.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ConstructionPrototype> Recipe;

    /// <summary>
    /// Position relative to the template origin.
    /// </summary>
    [DataField]
    public Vector2 Offset;

    /// <summary>
    /// Direction relative to the template's south-facing orientation.
    /// </summary>
    [DataField]
    public Direction Direction = Direction.South;

    /// <summary>
    /// Player-written text attached to a comment ghost.
    /// </summary>
    [DataField]
    public string Comment = string.Empty;
}

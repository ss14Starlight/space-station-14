using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Hand-tuned AABB crop of a station map into a tutorial room section template.
/// </summary>
[Prototype] //Tutorial: drop redundant type (RA0042)
public sealed partial class TutorialSectionCropPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Station (or other) map to crop from.
    /// </summary>
    [DataField(required: true)]
    public ResPath SourceMap = default!;

    /// <summary>
    /// Output grid path under Resources (e.g. /Maps/_Functional/TutorialServer/Sections/Medbay.yml).
    /// </summary>
    [DataField(required: true)]
    public ResPath Output = default!;

    /// <summary>
    /// Matching <see cref="TutorialRoomTemplatePrototype"/> id (for docs / wiring).
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TutorialRoomTemplatePrototype> TemplateId;

    /// <summary>
    /// Bottom-left tile of the crop on the source grid (inclusive).
    /// </summary>
    [DataField(required: true)]
    public Vector2i Origin;

    /// <summary>
    /// Width,height in tiles (inclusive span from Origin).
    /// </summary>
    [DataField(required: true)]
    public Vector2i Size;

    [DataField]
    public EntProtoId GateDoor = "Airlock";

    [DataField]
    public string? SourceNote;
}

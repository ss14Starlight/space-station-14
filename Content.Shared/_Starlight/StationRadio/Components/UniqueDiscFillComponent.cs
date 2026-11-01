using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.StationRadio.Components;

[RegisterComponent]
public sealed partial class UniqueDiscFillComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Table;

    [DataField]
    public int Min = 4;

    [DataField]
    public int Max = 8;

    /// <summary>
    /// Sleeve or case each disc is put in; the disc is filed bare when null.
    /// </summary>
    [DataField]
    public EntProtoId? Wrapper;

    /// <summary>
    /// Container the wrapped discs go into.
    /// </summary>
    [DataField(required: true)]
    public string Container = string.Empty;
}

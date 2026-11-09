using Content.Shared.Paper;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Fax;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class PaperData
{
    [DataField] public string? Name;
    [DataField] public string? Content;
    [DataField] public string? StampState;
    [DataField] public List<StampDisplayInfo> StampedBy = [];
    [DataField] public bool EditingDisabled;
    [DataField] public string? SentBy;
    [DataField] public bool IncludeMeta = true;
}

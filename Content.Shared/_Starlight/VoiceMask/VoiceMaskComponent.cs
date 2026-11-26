using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared.VoiceMask;

public sealed partial class VoiceMaskComponent
{
    /// <summary>
    /// Job icon shown next to the name on the radio while the mask is active.
    /// </summary>
    [DataField]
    public ProtoId<JobIconPrototype>? RadioJobIcon;
}

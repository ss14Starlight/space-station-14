using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.StatusIcon;

/// <summary>
/// Fixed job identity for entities with no ID card to read one from
/// (jobEntity-spawned mobs like K9, Borg, etc). Bypasses the normal
/// ID-card/PDA lookup for the above-head job icon and, if JobTitle is set,
/// for crew monitoring's name/job display too.
/// </summary>
[RegisterComponent]
public sealed partial class FixedJobIconComponent : Component
{
    [DataField(required: true)] public ProtoId<JobIconPrototype> JobIcon;

    /// <summary>
    /// Job title shown on crew monitoring/suit sensors. Optional - if unset,
    /// those UIs keep their own no-ID-card fallback text.
    /// </summary>
    [DataField] public LocId? JobTitle;
}

using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.StatusIcon;

/// <summary>
/// Shows a fixed job icon above the entity's head, bypassing the normal
/// ID-card/PDA lookup. For jobEntity-spawned mobs (K9, Borg, etc.) that have
/// no "id" inventory slot to read a job icon from.
/// </summary>
[RegisterComponent]
public sealed partial class FixedJobIconComponent : Component
{
    [DataField(required: true)] public ProtoId<JobIconPrototype> JobIcon;
}

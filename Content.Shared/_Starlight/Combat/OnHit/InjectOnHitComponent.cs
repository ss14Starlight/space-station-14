using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;

namespace Content.Shared.Damage.Components;

[RegisterComponent]
public sealed partial class InjectOnHitComponent : Component
{
    [DataField("reagents")]
    public List<ReagentQuantity> Reagents;
    
    [DataField("limit")]
    public float? ReagentLimit;

    [DataField("sound")]
    public SoundSpecifier? Sound;

    [DataField]
    public bool RequireIncapacitated = false;
}
[ByRefEvent]
public record struct InjectOnHitAttemptEvent(bool Cancelled);

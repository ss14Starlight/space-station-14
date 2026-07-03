using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
namespace Content.Shared._Starlight.Combat.OnHit;
[RegisterComponent]
public sealed partial class InjectOnHitComponent : Component
{
    [DataField("reagents")]
    public List<ReagentQuantity> Reagents;

    [DataField("limit")]
    public float? ReagentLimit;
    [DataField("sound")]
    public SoundSpecifier? Sound;
}
[ByRefEvent]
public record struct InjectOnHitAttemptEvent(bool Cancelled, EntityUid? Attacker = null);

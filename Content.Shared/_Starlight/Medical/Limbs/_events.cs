namespace Content.Shared._Starlight.Medical.Limbs;

[ByRefEvent]
public record struct LimbAttachedEvent
{
    public EntityUid Limb;
    public EntityUid Body;
}

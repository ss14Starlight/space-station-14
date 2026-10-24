using Content.Shared.Body.Part;

namespace Content.Shared._Starlight.Medical.Body.Part;

[ByRefEvent]
public readonly record struct BodyPartAddedEvent(string Slot, Entity<BodyPartComponent> Part);

[ByRefEvent]
public record struct BodyPartRemovedEvent(string Slot, Entity<BodyPartComponent> Part)
{
    public bool PreventVitalDamage;
}

namespace Content.Shared._Starlight.Weapons.Melee.Events;

// Allows for a melee attack to be treated as originating from another entity
public sealed class GetMeleeOriginEvent : HandledEntityEventArgs
{
    public EntityUid? OriginEntity = null;
}

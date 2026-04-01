using Content.Shared.Damage;

namespace Content.Shared._FarHorizons.Damage;

public sealed class HealModifyEvent : EntityEventArgs
{
    public DamageSpecifier Damage;
    public EntityUid? Origin;

    public HealModifyEvent(DamageSpecifier damage, EntityUid? origin = null)
    {
        Damage = damage;
        Origin = origin;
    }
}
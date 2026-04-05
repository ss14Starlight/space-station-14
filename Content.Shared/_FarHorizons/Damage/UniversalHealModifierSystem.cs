namespace Content.Shared._FarHorizons.Damage;

public sealed class UniversalHealModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalHealModifierComponent, HealModifyEvent>(OnHealModify);
    }

    private void OnHealModify(Entity<UniversalHealModifierComponent> ent, ref HealModifyEvent args)
    {
        foreach (var (key, value) in args.Damage.DamageDict)
            if (value < 0)
                args.Damage.DamageDict[key] *= ent.Comp.Modifier;
    }
}

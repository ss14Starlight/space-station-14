using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Temperature.Components;

namespace Content.Shared._Starlight.Traits.Assorted;

public sealed partial class TemperatureSensitiveSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnStartup(Entity<TemperatureSensitiveComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<TemperatureDamageComponent>(ent, out var temp))
            return;

        ent.Comp.OriginalHeatDamage = new DamageSpecifier(temp.HeatDamage);
        ent.Comp.OriginalColdDamage = new DamageSpecifier(temp.ColdDamage);
        temp.HeatDamage *= ent.Comp.HeatTemperatureMultiplier;
        temp.ColdDamage *= ent.Comp.ColdTemperatureMultiplier;
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<TemperatureSensitiveComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<TemperatureDamageComponent>(ent, out var temp))
            return;

        if (ent.Comp.OriginalHeatDamage != null)
            temp.HeatDamage = ent.Comp.OriginalHeatDamage;

        if (ent.Comp.OriginalColdDamage != null)
            temp.ColdDamage = ent.Comp.OriginalColdDamage;
    }

    [SubscribeLocalEvent]
    private void OnDamageModify(EntityUid uid, TemperatureSensitiveComponent comp, DamageModifyEvent args)
        => args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, comp.Modifiers, args.ArmorPenetration, args.CanHeal);
}

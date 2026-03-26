using Content.Shared.Armor;
using Content.Shared.Atmos;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;

namespace Content.Shared.Clothing.EntitySystems;

/// <summary>
/// Handles reducing fire damage when wearing clothing with <see cref="FireProtectionComponent"/>.
/// </summary>
public sealed class FireProtectionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FireProtectionComponent, InventoryRelayedEvent<GetFireProtectionEvent>>(OnGetProtection);
        SubscribeLocalEvent<FireProtectionComponent, ArmorExamineEvent>(OnArmorExamine);
    }

    private void OnGetProtection(Entity<FireProtectionComponent> ent, ref InventoryRelayedEvent<GetFireProtectionEvent> args)
    {
        args.Args.Reduce(ent.Comp.Reduction);
    }

    private void OnArmorExamine(Entity<FireProtectionComponent> ent, ref ArmorExamineEvent args)
    {
        var value = MathF.Round(ent.Comp.Reduction * 100, 1);

        if (value == 0)
            return;

        args.Msg.PushNewline();
        args.Msg.AddMarkupOrThrow(Loc.GetString(ent.Comp.ExamineMessage, ("value", value)));
    }
    
    // Starlight - begin
    /// <summary>
    /// Sets the damage reduction of the fire protection component.
    /// </summary>
    /// <param name="component">The fire protection component.</param>
    /// <param name="reduction">The percentage (0.0 - 1.0) that fire damage is reduced by.</param>
    public void SetFireProtection(FireProtectionComponent component, float reduction) =>
        component.Reduction = reduction;
    // Starlight - end
}

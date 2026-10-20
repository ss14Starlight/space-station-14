using Content.Shared._Starlight.PDA.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Clothing;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;

namespace Content.Shared._Starlight.PDA;

/// <inheritdoc cref="SyncIdNameToWearerComponent"/>
public sealed partial class SyncIdNameToWearerSystem : EntitySystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        // After ItemSlotsSystem has spawned the PDA's starting ID.
        SubscribeLocalEvent<SyncIdNameToWearerComponent, MapInitEvent>(OnMapInit, after: [typeof(ItemSlotsSystem)]);
        SubscribeLocalEvent<SyncIdNameToWearerComponent, ClothingGotEquippedEvent>(OnEquipped);
    }

    private void OnMapInit(Entity<SyncIdNameToWearerComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<PdaComponent>(ent, out var pda))
            return;

        ent.Comp.OriginalId = pda.ContainedId;
        Dirty(ent);
    }

    private void OnEquipped(Entity<SyncIdNameToWearerComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (ent.Comp.OriginalId is not { } original
            || !TryComp<PdaComponent>(ent, out var pda)
            || pda.ContainedId != original
            || !TryComp<IdCardComponent>(original, out var idCard))
            return;

        _idCard.TryChangeFullName(original, MetaData(args.Wearer).EntityName, idCard);
    }
}

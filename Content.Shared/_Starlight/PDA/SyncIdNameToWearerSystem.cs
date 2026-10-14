using Content.Shared._Starlight.PDA.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Clothing;

namespace Content.Shared._Starlight.PDA;

/// <inheritdoc cref="SyncIdNameToWearerComponent"/>
public sealed partial class SyncIdNameToWearerSystem : EntitySystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SyncIdNameToWearerComponent, ClothingGotEquippedEvent>(OnEquipped);
    }

    private void OnEquipped(Entity<SyncIdNameToWearerComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!_idCard.TryGetIdCard(ent.Owner, out var idCard))
            return;

        var wearerName = MetaData(args.Wearer).EntityName;
        _idCard.TryChangeFullName(idCard, wearerName, idCard.Comp);
    }
}

using Content.Shared.Eye;
using Robust.Server.GameObjects;
using Content.Shared.Inventory.Events;
using Content.Shared.Clothing.Components;
using Content.Shared._Starlight.NullSpace;

namespace Content.Server._Starlight.NullSpace;

public sealed partial class ShowNullSpaceSystem : SharedShowNullSpaceSystem
{
    [Dependency] private readonly EyeSystem _eye = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShowNullSpaceComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ShowNullSpaceComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ShowNullSpaceComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ShowNullSpaceComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ShowNullSpaceComponent, GetVisMaskEvent>(OnGetVisMask);
    }

    private void OnGetVisMask(Entity<ShowNullSpaceComponent> uid, ref GetVisMaskEvent args) =>
        args.VisibilityMask |= (int)VisibilityFlags.NullSpace;

    private void OnInit(EntityUid uid, ShowNullSpaceComponent component, MapInitEvent args) =>
        _eye.RefreshVisibilityMask(uid);

    public void OnRemove(EntityUid uid, ShowNullSpaceComponent component, ComponentRemove args) =>
        _eye.RefreshVisibilityMask(uid);

    private void OnEquipped(EntityUid uid, ShowNullSpaceComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing)
            || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        EntityManager.CopyComponent(uid, args.Equipee, component);
    }

    private void OnUnequipped(EntityUid uid, ShowNullSpaceComponent component, GotUnequippedEvent args) =>
        RemComp<ShowNullSpaceComponent>(args.Equipee);
}

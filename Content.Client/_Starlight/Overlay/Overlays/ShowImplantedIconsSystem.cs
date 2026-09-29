using Content.Shared._Starlight.Overlay.Components;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Content.Client.Overlays;

namespace Content.Client._Starlight.Overlay.Overlays;

public sealed partial class ShowImplantedIconsSystem : EquipmentHudSystem<ShowImplantedIconsComponent>
{
    [Dependency] private IPrototypeManager _prototype = default!;

    [ViewVariables]
    public HashSet<string> ShownIconTypes = new();
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImplantedIconComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

        protected override void UpdateInternal(RefreshEquipmentHudEvent<ShowImplantedIconsComponent> component)
    {
        base.UpdateInternal(component);

        ShownIconTypes.Clear();
        foreach (var comp in component.Components)
        {
            foreach (var iconType in comp.ShownIcons)
            {
                ShownIconTypes.Add(iconType);
            }
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        ShownIconTypes.Clear();
    }
    private void OnGetStatusIconsEvent(EntityUid uid, ImplantedIconComponent component, ref GetStatusIconsEvent args)
    {
        if (!IsActive || !ShownIconTypes.Contains(component.IconType))
            return;

        if (_prototype.TryIndex<FactionIconPrototype>(component.Icon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}

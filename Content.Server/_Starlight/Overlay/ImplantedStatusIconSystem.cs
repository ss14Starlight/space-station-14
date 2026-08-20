using Content.Shared.Implants;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared._Starlight.Implants.Components;

namespace Content.Server._Starlight.Overlay;

/// <summary>
/// System used for adding or removing components for showing status icons based on an implant.
/// </summary>
public sealed partial class ImplantedStatusIconSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IconImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<IconImplantComponent, ImplantRemovedEvent>(OnImplantRemoved);
    }
    private void OnImplantImplanted(Entity<IconImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        var comp = EnsureComp<ImplantedIconComponent>(args.Implanted);
        comp.Icon = ent.Comp.Icon;
        comp.IconType = ent.Comp.IconType;
        Dirty(args.Implanted, comp);
    }
    private void OnImplantRemoved(Entity<IconImplantComponent> ent, ref ImplantRemovedEvent args) => RemComp<ImplantedIconComponent>(args.Implanted);
}

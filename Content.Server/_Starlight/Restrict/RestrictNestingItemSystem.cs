using Content.Shared.Disposal.Components;
using Content.Shared.Popups;
using Content.Shared._Starlight.Restrict;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Server._Starlight.Restrict;

public sealed partial class RestrictNestingItemSystem : SharedRestrictNestingItemSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DisposalUnitComponent, BeforeDisposalFlushEvent>(OnDisposalFlush);
    }

    /// <summary>
    /// Blocks you from mailing small species like felionoids and resomi by stuffing them in mailing units.
    /// </summary>
    private void OnDisposalFlush(Entity<DisposalUnitComponent> ent, ref BeforeDisposalFlushEvent args)
    {
        // only apply to mailing units
        if (!HasComp<MailingUnitComponent>(ent))
            return;

        if (RecursivelyCheckForNesting(ent, skipInitialItem: false))
        {
            _popup.PopupEntity(
                Loc.GetString("restrict-nesting-item-failed-to-flush-mailing"),
                ent);

            args.Cancel();

            // eject contents
            if (ent.Comp.Container != null)
            {
                foreach(var item in ent.Comp.Container.ContainedEntities.ToArray())
                {
                    _container.Remove(item, ent.Comp.Container);
                }
            }
        }
    }
}

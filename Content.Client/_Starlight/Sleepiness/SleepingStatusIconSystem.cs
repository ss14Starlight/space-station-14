using Content.Shared.Bed.Sleep;
using Content.Shared._Starlight.CosmicCult.Components.Examine;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Sleepiness;

public sealed partial class SleepingStatusIconSystem : EntitySystem
{
    private static readonly ProtoId<SsdIconPrototype> _sleepIcon = "SleepIcon";

    [Dependency] private IPrototypeManager _prototype = default!;

    [SubscribeLocalEvent]
    private void OnGetStatusIcon(EntityUid uid, SleepingComponent component, ref GetStatusIconsEvent args)
    {
        if (IsSleepIconSuppressed(uid))
            return;

        args.StatusIcons.Add(_prototype.Index(_sleepIcon));
    }

    [SubscribeLocalEvent]
    private void OnContainerGetStatusIcons(EntityUid uid, ContainerManagerComponent component, ref GetStatusIconsEvent args)
    {
        if (HasComp<SleepingComponent>(uid) && !IsSleepIconSuppressed(uid))
            return;

        foreach (var container in component.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (!HasComp<SleepingComponent>(contained) || IsSleepIconSuppressed(contained))
                    continue;

                args.StatusIcons.Add(_prototype.Index(_sleepIcon));
                return;
            }
        }
    }

    private bool IsSleepIconSuppressed(EntityUid uid) =>
        (TryComp<SSDIndicatorComponent>(uid, out var ssd) && ssd.IsSSD)
        || HasComp<CosmicBlankComponent>(uid);
}

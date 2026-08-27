using Content.Shared.Hands.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;

namespace Content.Shared.Hands.EntitySystems;

/// <summary>
/// This is for events that don't affect normal hand functions but do care about hands.
/// </summary>
public abstract partial class SharedHandsSystem
{
    private void InitializeEventListeners()
    {
        SubscribeLocalEvent<HandsComponent, GetStandUpTimeEvent>(OnStandupArgs);
        SubscribeLocalEvent<HandsComponent, KnockedDownRefreshEvent>(OnKnockedDownRefresh);
    }

    /// <summary>
    /// Reduces the time it takes to stand up based on the number of hands we have available.
    /// </summary>
    private void OnStandupArgs(Entity<HandsComponent> ent, ref GetStandUpTimeEvent time)
    {
        if (!HasComp<KnockedDownComponent>(ent))
            return;

        var hands = GetEmptyHandCount(ent.Owner);

        if (hands == 0)
            return;

        time.DoAfterTime *= (float)ent.Comp.Count / (hands + ent.Comp.Count);
    }

    private void OnKnockedDownRefresh(Entity<HandsComponent> ent, ref KnockedDownRefreshEvent args)
    {
        var freeHands = CountFreeHands(ent.AsNullable());
        var totalHands = GetHandCount(ent.AsNullable());

        // Can't crawl around without any hands.
        // Entities without the HandsComponent will always have full crawling speed.
        if (totalHands == 0)
        {
            // Starlight BEGIN: Allow cyborgs to crawl without hands at normal crawling speed.
            if (HasComp<BorgChassisComponent>(ent.Owner))
            {
                if (TryComp<CrawlerComponent>(ent.Owner, out var crawler))
                    args.SpeedModifier = crawler.SpeedModifier;
                else
                    args.SpeedModifier = 1f;
                return;
            }
            args.SpeedModifier = 0f;
        }
        else
        {
            // Decouples cyborgs from crawling speed changes based on hands.
            // Cyborgs with a module out will crawl at half the speed regardless of open hands.
            if (HasComp<BorgChassisComponent>(ent.Owner))
            {
                args.SpeedModifier *= 0.5f;
                return;
            }
            args.SpeedModifier *= (float)freeHands / totalHands;
        } // Starlight END
    }
}

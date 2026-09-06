using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stunnable;

namespace Content.Shared._Starlight.Stunnable;

/// <summary>
/// Handles <see cref="CanCrawlWithoutHandsComponent"/> - overrides the default
/// hands-required crawl block when the entity has zero hands.
/// Runs after <see cref="SharedHandsSystem"/> to replace the 0 speed with normal crawl speed.
/// </summary>
public sealed partial class CanCrawlWithoutHandsSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CanCrawlWithoutHandsComponent, KnockedDownRefreshEvent>(OnRefresh,
            after: new[] { typeof(SharedHandsSystem), typeof(SharedStunSystem) });
    }

    private void OnRefresh(Entity<CanCrawlWithoutHandsComponent> ent, ref KnockedDownRefreshEvent args)
    {
        if (!TryComp<HandsComponent>(ent.Owner, out var hands))
            return;

        var total = _hands.GetHandCount((ent.Owner, hands));
        if (total != 0)
            return;

        if (TryComp<CrawlerComponent>(ent.Owner, out var crawler))
            args.SpeedModifier = crawler.SpeedModifier;
        else
            args.SpeedModifier = 1f;
    }
}

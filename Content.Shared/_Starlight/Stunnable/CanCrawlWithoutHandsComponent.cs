using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stunnable;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Stunnable;

/// <summary>
/// Allows an entity to crawl even without hands.
/// Add this component to any entity that should be able to crawl with zero hands,
/// such as cyborgs when no module is selected.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CanCrawlWithoutHandsComponent : Component
{
}

/// <summary>
/// Handles <see cref="CanCrawlWithoutHandsComponent"/> - overrides the default
/// hands-required crawl block when the entity has zero hands.
/// Runs after <see cref="SharedHandsSystem"/> to replace the 0 speed with normal crawl speed.
/// </summary>
public sealed class CanCrawlWithoutHandsSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CanCrawlWithoutHandsComponent, KnockedDownRefreshEvent>(OnRefresh,
            after: new[] { typeof(SharedHandsSystem) });
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

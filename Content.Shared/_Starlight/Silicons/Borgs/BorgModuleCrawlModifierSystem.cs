using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Handles crawling speed for cyborgs with <see cref="BorgModuleCrawlModifierComponent"/>.
/// When any module is active, crawl speed is set to a fixed multiplier instead of scaling with free hands.
/// </summary>
public sealed class BorgModuleCrawlModifierSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgModuleCrawlModifierComponent, KnockedDownRefreshEvent>(OnKnockedDownRefresh,
            after: new[] { typeof(SharedHandsSystem), typeof(SharedStunSystem) });
    }

    private void OnKnockedDownRefresh(Entity<BorgModuleCrawlModifierComponent> ent, ref KnockedDownRefreshEvent args)
    {
        if (!TryComp<BorgChassisComponent>(ent.Owner, out _))
            return;

        if (!TryComp<HandsComponent>(ent.Owner, out var hands))
            return;

        // Cyborgs have no hands without a module. We use hand count to detect active modules
        // instead of SelectedModule to avoid a timing issue where HandCountChanged fires
        // before SelectedModule is set during ProvideItems.
        if (hands.Hands.Count == 0)
            return;

        // Overrides the normal hand-based movement speed penalty.
        // If a cyborg has a module out, apply ActiveSpeedModifier.
        float crawlerMod = 1f;
        if (TryComp<CrawlerComponent>(ent.Owner, out var crawler))
            crawlerMod = crawler.SpeedModifier;

        args.SpeedModifier = crawlerMod * ent.Comp.ActiveSpeedModifier;
    }
}

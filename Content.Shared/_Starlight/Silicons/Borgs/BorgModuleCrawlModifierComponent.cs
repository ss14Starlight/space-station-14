using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// When attached to a cyborg chassis, modifies crawling speed when a module is active.
/// Decouples crawl speed from hand count and applies a fixed multiplier instead.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BorgModuleCrawlModifierComponent : Component
{
    /// <summary>
    /// Speed multiplier applied while knocked down and a cyborg module is selected.
    /// </summary>
    [DataField]
    public float ActiveSpeedModifier = 0.5f;
}

/// <summary>
/// Handles crawling speed for cyborgs with <see cref="BorgModuleCrawlModifierComponent"/>.
/// When any module is active, crawl speed is set to a fixed multiplier instead of scaling with free hands.
/// </summary>
public sealed class BorgModuleCrawlModifierSystem : EntitySystem
{
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

        // Cyborgs has no hands without a module. We use hand count to detect active modules
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

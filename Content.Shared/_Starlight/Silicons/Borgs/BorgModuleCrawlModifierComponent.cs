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
    /// Speed multiplier applied while knocked down and a borg module is selected.
    /// 0.5 = half normal crawling speed.
    /// </summary>
    [DataField]
    public float ActiveSpeedModifier = 0.5f;
}

/// <summary>
/// Handles crawling speed for borgs with <see cref="BorgModuleCrawlModifierComponent"/>.
/// When any module is active (SelectedModule != null), crawl speed is set to a fixed
/// multiplier instead of scaling with free hands, preventing hand system interference.
/// Runs after Hands and Stun systems to completely decouple from SharedHandsSystem.EventListeners.cs.
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
        if (!TryComp<BorgChassisComponent>(ent.Owner, out var chassis))
            return;

        if (chassis.SelectedModule == null)
            return;

        // Completely override hand-ratio contribution: fixed half speed (plus crawler multiplier).
        // This decouples borg logic from SharedHandsSystem.EventListeners.cs - that file now only handles
        // generic CanCrawlWithoutHands + freeHands/totalHands ratio.
        float crawlerMod = 1f;
        if (TryComp<CrawlerComponent>(ent.Owner, out var crawler))
            crawlerMod = crawler.SpeedModifier;

        args.SpeedModifier = crawlerMod * ent.Comp.ActiveSpeedModifier;
    }
}

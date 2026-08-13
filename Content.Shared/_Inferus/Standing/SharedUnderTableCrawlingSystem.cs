using Content.Shared.ActionBlocker;
using Content.Shared.Input;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Inferus.Standing;

/// <summary>
/// Handles requests to toggle "crawling under furniture" while downed
/// Draw depth is updated client-side
/// </summary>
public sealed class SharedUnderTableCrawlingSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleCrawlingUnder, InputCmdHandler.FromDelegate(HandleCrawlUnderRequest, handle: false))
            .Register<SharedUnderTableCrawlingSystem>();

        SubscribeLocalEvent<UnderTableCrawlingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<UnderTableCrawlingComponent, DownedEvent>(OnDowned);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<SharedUnderTableCrawlingSystem>();
    }

    private void HandleCrawlUnderRequest(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } uid)
            return;

        if (!TryComp<StandingStateComponent>(uid, out var standingState)
            || !TryComp<UnderTableCrawlingComponent>(uid, out var underCrawl)
            || !_actionBlocker.CanConsciouslyPerformAction(uid))
            return;

        var newState = !underCrawl.IsCrawlingUnder;
        // If standing, only force off (fallback to fix draw depth)
        if (standingState.Standing)
            newState = false;

        underCrawl.IsCrawlingUnder = newState;
        _speed.RefreshMovementSpeedModifiers(uid);
        Dirty(uid, underCrawl);
    }

    private void OnRefreshMovementSpeed(EntityUid uid, UnderTableCrawlingComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!_standing.IsDown(uid))
            return;

        var modifier = component.IsCrawlingUnder ? component.CrawlingUnderSpeedModifier : 1f;
        args.ModifySpeed(modifier, modifier);
    }

    private void OnDowned(Entity<UnderTableCrawlingComponent> ent, ref DownedEvent args)
    {
        // After downing, default to NOT under furniture
        if (_timing is { ApplyingState: false, IsFirstTimePredicted: true })
            ent.Comp.IsCrawlingUnder = false;
    }
}

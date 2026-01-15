using Content.Shared.DoAfter;
using Robust.Shared.Log;

namespace Content.Shared._Starlight.DoAfterSpeedModifier;

/// <summary>
/// Handles modifying DoAfter action speeds for entities with DoAfterSpeedModifierComponent.
/// Does NOT apply to excluded event types specified in the component.
/// </summary>
public sealed class DoAfterSpeedModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoAfterSpeedModifierComponent, DoAfterStartModifyEvent>(OnDoAfterStartModify);
    }

    private void OnDoAfterStartModify(EntityUid uid, DoAfterSpeedModifierComponent component, ref DoAfterStartModifyEvent args)
    {
        if (component.SpeedModifier <= 0 || component.SpeedModifier == 1.0f)
            return;

        // Check if this event type should be excluded from speed modification
        var eventType = args.Args.Event?.GetType().FullName;
        if (eventType != null && component.ExcludedEvents.Contains(eventType))
            return;

        var originalDelay = args.Args.Delay;
        
        // Modify the delay by dividing by the speed modifier
        // A multiplier of 1.10 means 10% faster (delay / 1.10 = ~0.91x the original delay)
        args.Args.Delay /= component.SpeedModifier;
        
        // Debug logging to verify it's working
        Log.Debug($"DoAfter speed modified for {ToPrettyString(uid)}: {originalDelay.TotalSeconds:F2}s -> {args.Args.Delay.TotalSeconds:F2}s (modifier: {component.SpeedModifier})");
    }
}

/// <summary>
/// Raised before a DoAfter is started, allowing systems to modify the args.
/// This is not a cancellable event, just for modifying DoAfter parameters.
/// </summary>
[ByRefEvent]
public record struct DoAfterStartModifyEvent(DoAfterArgs Args);

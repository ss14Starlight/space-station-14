using Content.Shared.DoAfter;
using Content.Shared._Starlight.Silicons;
using Robust.Shared.Log;

namespace Content.Shared._Starlight.DoAfterSpeedModifier;

/// <summary>
/// Handles modifying DoAfter action speeds for entities with DoAfterSpeedModifierComponent.
/// Does NOT apply to self-healing actions (welder/cable repairs).
/// </summary>
public sealed class DoAfterSpeedModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoAfterSpeedModifierComponent, DoAfterStartAttemptEvent>(OnDoAfterStartAttempt);
    }

    private void OnDoAfterStartAttempt(EntityUid uid, DoAfterSpeedModifierComponent component, ref DoAfterStartAttemptEvent args)
    {
        if (component.SpeedModifier <= 0 || component.SpeedModifier == 1.0f)
            return;

        // Don't apply speed buff to self-healing (welding/cable repairs)
        // This prevents IPCs from healing themselves faster
        if (args.Args.Event is WelderHealingDoAfterEvent)
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
/// </summary>
[ByRefEvent]
public record struct DoAfterStartAttemptEvent(DoAfterArgs Args);

using Content.Shared.DoAfter;

namespace Content.Shared._Starlight.DoAfterSpeedModifier;

/// <summary>
/// Handles modifying DoAfter action speeds for entities with DoAfterSpeedModifierComponent.
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

        // Modify the delay by dividing by the speed modifier
        // A multiplier of 1.2 means 20% faster (delay / 1.2 = ~0.833x the original delay)
        args.Args.Delay /= component.SpeedModifier;
    }
}

/// <summary>
/// Raised before a DoAfter is started, allowing systems to modify the args.
/// </summary>
[ByRefEvent]
public record struct DoAfterStartAttemptEvent(DoAfterArgs Args);

using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

/// <summary>
/// Applies movement slowdown when a vampire is starving
/// </summary>
public sealed class SharedVampireStarvationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnRefreshMovespeed(EntityUid uid, VampireComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.BloodFullness > 0f)
            return;

        args.ModifySpeed(component.StarvationWalkSpeedModifier, component.StarvationSprintSpeedModifier);
    }
}

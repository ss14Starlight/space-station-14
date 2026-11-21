using Content.Server.Administration.Logs;
using Content.Server._Starlight.Implants.Components;
using Content.Shared.Database;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;

namespace Content.Server._Starlight.Implants;

/// <summary>
/// Handles admin logging for budget bluespace implant activations,
/// explicitly separating manual channeling from automatic death triggers.
/// </summary>
public sealed partial class BudgetBluespaceImplantSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BudgetBluespaceImplantComponent, ActivateImplantEvent>(OnManualActivate);
        SubscribeLocalEvent<BudgetBluespaceImplantComponent, ImplantRelayEvent<MobStateChangedEvent>>(OnDeathRelay);
    }

    /// <summary>
    /// Logs when a player manually channels and activates the implant action.
    /// </summary>
    private void OnManualActivate(Entity<BudgetBluespaceImplantComponent> ent, ref ActivateImplantEvent args)
    {
        var coords = _transform.GetMoverCoordinates(args.Performer);
        _adminLogger.Add(LogType.Trigger, LogImpact.High,
            $"{ToPrettyString(args.Performer):user} manually activated {ToPrettyString(ent):implant} at {coords}.");
    }

    /// <summary>
    /// Logs when the implant triggers automatically upon the host entering the Dead state.
    /// </summary>
    private void OnDeathRelay(Entity<BudgetBluespaceImplantComponent> ent, ref ImplantRelayEvent<MobStateChangedEvent> args)
    {
        if (args.Args.NewMobState != MobState.Dead)
            return;

        var coords = _transform.GetMoverCoordinates(args.ImplantedEntity);
        _adminLogger.Add(LogType.Trigger, LogImpact.High,
            $"{ToPrettyString(args.ImplantedEntity):user}'s {ToPrettyString(ent):implant} triggered automatically upon death at {coords}.");
    }
}


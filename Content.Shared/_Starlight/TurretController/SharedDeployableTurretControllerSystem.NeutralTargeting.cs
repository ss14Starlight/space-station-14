using Content.Shared._Starlight.TurretController;
using Content.Shared.Turrets;

// ReSharper disable once CheckNamespace
namespace Content.Shared.TurretController;

public abstract partial class SharedDeployableTurretControllerSystem
{
    private void InitializeNeutralTargeting() =>
        SubscribeLocalEvent<DeployableTurretControllerComponent, DeployableTurretNeutralTargetingChangedMessage>(OnNeutralTargetingChanged);

    private void OnNeutralTargetingChanged(
        Entity<DeployableTurretControllerComponent> ent,
        ref DeployableTurretNeutralTargetingChangedMessage args)
    {
        if (IsUserAllowedAccess(ent, args.Actor))
            ChangeNeutralTargeting(ent, args.Enabled, args.Actor);

        if (_userInterfaceSystem.TryGetOpenUi(ent.Owner, DeployableTurretControllerUiKey.Key, out var bui))
            bui.Update<DeployableTurretControllerBoundInterfaceState>();
    }

    protected virtual void ChangeNeutralTargeting(
        Entity<DeployableTurretControllerComponent> ent,
        bool enabled,
        EntityUid? user = null)
    {
        if (!TryComp<TurretTargetSettingsComponent>(ent, out var targetSettings))
            return;

        _turretTargetingSettings.SetTargetNeutralEntities((ent, targetSettings), enabled);
    }
}

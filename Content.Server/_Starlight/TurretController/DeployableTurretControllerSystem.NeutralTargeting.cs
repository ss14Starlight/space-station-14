using System.Linq;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.TurretController;
using Content.Shared.Turrets;
using Robust.Shared.Map.Events;

// ReSharper disable once CheckNamespace
namespace Content.Server.TurretController;

public sealed partial class DeployableTurretControllerSystem
{
    public const string CmdSetNeutralTargeting = "set_neutral_targeting";

    [SubscribeLocalEvent(after: [typeof(DeviceNetworkSystem)])]
    private void OnMapInit(Entity<DeployableTurretControllerComponent> ent, ref MapInitEvent args)
    {
        var controllers = GetSynchronizedControllerGroup(ent);
        var source = controllers
            .OrderBy(controller => controller.Comp.ReadOnly)
            .ThenBy(controller => controller.Owner)
            .First();

        if (!TryComp<TurretTargetSettingsComponent>(source, out var sourceSettings))
            return;

        var targetNeutralEntities = sourceSettings.TargetNeutralEntities;
        var needsSynchronization = false;
        foreach (var controller in controllers)
        {
            if (!TryComp<TurretTargetSettingsComponent>(controller, out var targetSettings) ||
                targetSettings.TargetNeutralEntities == targetNeutralEntities)
            {
                continue;
            }

            needsSynchronization = true;
            break;
        }

        if (needsSynchronization)
        {
            ChangeNeutralTargeting(source, targetNeutralEntities);
            return;
        }

        if (TryComp<DeviceNetworkComponent>(ent, out var device))
            SendNeutralTargetingSetting(ent, null, device);
    }

    private void SendNeutralTargetingSetting(
        Entity<DeployableTurretControllerComponent> ent,
        string? address,
        DeviceNetworkComponent device)
    {
        if (!TryComp<TurretTargetSettingsComponent>(ent, out var targetSettings))
        {
            return;
        }

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = CmdSetNeutralTargeting,
            [CmdSetNeutralTargeting] = targetSettings.TargetNeutralEntities,
        };

        _deviceNetwork.QueuePacket(ent, address, payload, device: device);
    }

    protected override void ChangeNeutralTargeting(
        Entity<DeployableTurretControllerComponent> ent,
        bool enabled,
        EntityUid? user = null)
    {
        if (!TryComp<TurretTargetSettingsComponent>(ent, out _))
            return;

        base.ChangeNeutralTargeting(ent, enabled, user);
        SynchronizeNeutralTargeting(ent, enabled, user);

        if (!TryComp<DeviceNetworkComponent>(ent, out var device))
            return;

        _adminLogger.Add(
            LogType.ItemConfigure,
            LogImpact.Medium,
            $"{ToPrettyString(user)} set {ToPrettyString(ent)} neutral targeting to {enabled}");

        SendNeutralTargetingSetting(ent, null, device);
    }

    private void SynchronizeNeutralTargeting(
        Entity<DeployableTurretControllerComponent> ent,
        bool enabled,
        EntityUid? user)
    {
        var isRootChange = _controllersBeingSynchronized.Count == 0;
        if (!_controllersBeingSynchronized.Add(ent))
            return;

        try
        {
            foreach (var linkedController in GetSyncedControllers(ent))
            {
                if (_controllersBeingSynchronized.Contains(linkedController))
                    continue;

                ChangeNeutralTargeting(linkedController, enabled, user);
                UpdateUIState(linkedController);
            }
        }
        finally
        {
            if (isRootChange)
                _controllersBeingSynchronized.Clear();
        }
    }
}

using System.Linq;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.TurretController;
using Content.Shared.Turrets;

// ReSharper disable once CheckNamespace
namespace Content.Server.TurretController;

public sealed partial class DeployableTurretControllerSystem
{
    public const string CmdSetNeutralTargeting = "set_neutral_targeting";

    private void SendNeutralTargetingSetting(
        Entity<DeployableTurretControllerComponent> ent,
        string address,
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

        var payload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = CmdSetNeutralTargeting,
            [CmdSetNeutralTargeting] = enabled,
        };

        _adminLogger.Add(
            LogType.ItemConfigure,
            LogImpact.Medium,
            $"{ToPrettyString(user)} set {ToPrettyString(ent)} neutral targeting to {enabled}");

        _deviceNetwork.QueuePacket(ent, null, payload, device: device);
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

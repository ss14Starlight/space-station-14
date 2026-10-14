using System.Linq;
using Content.Shared.Access;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.TurretController;
using Content.Shared.Turrets;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.TurretController;

/// <summary>
/// Synchronizes the configuration of turret control panels connected through their Sync device link.
/// </summary>
public sealed partial class DeployableTurretControllerSystem
{
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;

    private const string SyncSourcePort = "TurretControllerSyncSender";
    private const string SyncSinkPort = "TurretControllerSyncReceiver";

    // A sync relationship is bidirectional even though device links have a source and a sink.
    // This guard allows changes to traverse a group of linked panels without cycling forever.
    private readonly HashSet<EntityUid> _controllersBeingSynchronized = new();

    private void InitializeSync()
        => SubscribeLocalEvent<DeployableTurretControllerComponent, NewLinkEvent>(OnNewSyncLink);

    private void OnNewSyncLink(Entity<DeployableTurretControllerComponent> ent, ref NewLinkEvent args)
    {
        if (ent.Owner != args.Sink ||
            args.SourcePort != SyncSourcePort ||
            args.SinkPort != SyncSinkPort ||
            !TryComp<DeployableTurretControllerComponent>(args.Source, out var sourceController) ||
            !TryComp<TurretTargetSettingsComponent>(args.Source, out var sourceTargetSettings) ||
            !TryComp<TurretTargetSettingsComponent>(ent, out var sinkTargetSettings))
        {
            return;
        }

        Entity<DeployableTurretControllerComponent> stateSource = (args.Source, sourceController);
        Entity<DeployableTurretControllerComponent> stateTarget = ent;
        Entity<TurretTargetSettingsComponent> sourceSettings = (args.Source, sourceTargetSettings);
        Entity<TurretTargetSettingsComponent> targetSettings = (ent, sinkTargetSettings);

        // A writable controller always seeds a read-only status panel, regardless of link direction.
        if (stateSource.Comp.ReadOnly && !stateTarget.Comp.ReadOnly)
        {
            (stateSource, stateTarget) = (stateTarget, stateSource);
            (sourceSettings, targetSettings) = (targetSettings, sourceSettings);
        }

        ChangeArmamentSetting(stateTarget, stateSource.Comp.ArmamentState, args.User);

        var exemptionsToDisable = targetSettings.Comp.ExemptAccessLevels
            .Except(sourceSettings.Comp.ExemptAccessLevels)
            .ToHashSet();
        var exemptionsToEnable = sourceSettings.Comp.ExemptAccessLevels
            .Except(targetSettings.Comp.ExemptAccessLevels)
            .ToHashSet();

        if (exemptionsToDisable.Count > 0)
            ChangeExemptAccessLevels(stateTarget, exemptionsToDisable, false, args.User);

        if (exemptionsToEnable.Count > 0)
            ChangeExemptAccessLevels(stateTarget, exemptionsToEnable, true, args.User);

        UpdateSynchronizedUIStates(stateTarget);
    }

    private void SynchronizeArmamentState(
        Entity<DeployableTurretControllerComponent> ent,
        int armamentState,
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

                ChangeArmamentSetting(linkedController, armamentState, user);
                UpdateUIState(linkedController);
            }
        }
        finally
        {
            if (isRootChange)
                _controllersBeingSynchronized.Clear();
        }
    }

    private void SynchronizeAccessExemptions(
        Entity<DeployableTurretControllerComponent> ent,
        HashSet<ProtoId<AccessLevelPrototype>> exemptions,
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

                ChangeExemptAccessLevels(linkedController, exemptions, enabled, user);
                UpdateUIState(linkedController);
            }
        }
        finally
        {
            if (isRootChange)
                _controllersBeingSynchronized.Clear();
        }
    }

    private HashSet<Entity<DeployableTurretControllerComponent>> GetSyncedControllers(
        Entity<DeployableTurretControllerComponent> ent)
    {
        var controllers = new HashSet<Entity<DeployableTurretControllerComponent>>();

        if (TryComp<DeviceLinkSourceComponent>(ent, out var source))
        {
            foreach (var sinkUid in _deviceLink.GetLinkedSinks((ent, source), SyncSourcePort))
            {
                if (TryComp<DeployableTurretControllerComponent>(sinkUid, out var controller))
                {
                    controllers.Add((sinkUid, controller));
                }
            }
        }

        if (TryComp<DeviceLinkSinkComponent>(ent, out var sinkComponent))
        {
            foreach (var sourceUid in sinkComponent.LinkedSources)
            {
                if (!TryComp<DeviceLinkSourceComponent>(sourceUid, out var linkedSource) ||
                    !_deviceLink.GetLinkedSinks((sourceUid, linkedSource), SyncSourcePort).Contains(ent) ||
                    !TryComp<DeployableTurretControllerComponent>(sourceUid, out var controller))
                {
                    continue;
                }

                controllers.Add((sourceUid, controller));
            }
        }

        return controllers;
    }

    /// <summary>
    /// Gets every controller connected to this controller through one or more Sync links.
    /// </summary>
    private HashSet<Entity<DeployableTurretControllerComponent>> GetSynchronizedControllerGroup(
        Entity<DeployableTurretControllerComponent> ent)
    {
        var controllers = new HashSet<Entity<DeployableTurretControllerComponent>> { ent };
        var pending = new Queue<Entity<DeployableTurretControllerComponent>>();
        pending.Enqueue(ent);

        while (pending.TryDequeue(out var controller))
        {
            foreach (var linkedController in GetSyncedControllers(controller))
            {
                if (!controllers.Add(linkedController))
                    continue;

                pending.Enqueue(linkedController);
            }
        }

        return controllers;
    }

    /// <summary>
    /// Refreshes every open UI in a synchronized controller group.
    /// </summary>
    private void UpdateSynchronizedUIStates(Entity<DeployableTurretControllerComponent> ent)
    {
        foreach (var controller in GetSynchronizedControllerGroup(ent))
            UpdateUIState(controller);
    }

    /// <summary>
    /// Builds the combined turret status list for every controller in the synchronized group.
    /// </summary>
    private Dictionary<string, string> GetSynchronizedTurretStates(
        Entity<DeployableTurretControllerComponent> ent)
    {
        var turretStates = new Dictionary<string, string>();

        foreach (var controller in GetSynchronizedControllerGroup(ent))
        {
            foreach (var (address, state) in controller.Comp.LinkedTurrets)
            {
                var stateName = state.ToString().ToLower();
                var stateDesc = Loc.GetString("turret-controls-window-turret-" + stateName);
                turretStates[address] = stateDesc;
            }
        }

        return turretStates;
    }
}

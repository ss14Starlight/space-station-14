using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Upgrades.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Upgrades;

public sealed partial class GunUpgradeSystem
{
    private void InitializeRemoval()
    {
        SubscribeLocalEvent<UpgradeableGunComponent, InteractUsingEvent>(OnInteractUsingRemoval);
        SubscribeLocalEvent<UpgradeableGunComponent, GunUpgradeRemovalFinishEvent>(OnUpgradeRemovalFinish);
    }

    private void OnInteractUsingRemoval(
        Entity<UpgradeableGunComponent> ent,
        ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ToolComponent>(args.Used, out var toolComp) ||
            !_tool.HasQuality(args.Used, ent.Comp.UpgradeRemovalMethod))
            return;

        if (!_container.TryGetContainer(
                ent.Owner,
                ent.Comp.UpgradesContainerId,
                out var upgradeContainer))
            return;

        if (!upgradeContainer.ContainedEntities.Any(HasComp<GunUpgradeComponent>))
        {
            _popup.PopupClient(
                Loc.GetString("gun-upgrade-removal-empty"),
                ent.Owner,
                args.User);

            args.Handled = true;
            return;
        }

        args.Handled = true;

        _tool.UseTool(
            args.Used,
            args.User,
            ent.Owner,
            ent.Comp.UpgradeRemovalDelay,
            ent.Comp.UpgradeRemovalMethod,
            new GunUpgradeRemovalFinishEvent(),
            toolComponent: toolComp);
    }

    private void OnUpgradeRemovalFinish(
        Entity<UpgradeableGunComponent> ent,
        ref GunUpgradeRemovalFinishEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_container.TryGetContainer(
                ent.Owner,
                ent.Comp.UpgradesContainerId,
                out var upgradeContainer))
            return;

        var upgrade = upgradeContainer.ContainedEntities
            .FirstOrDefault(uid => HasComp<GunUpgradeComponent>(uid));

        if (upgrade == default)
            return;

        if (!_container.TryRemoveFromContainer(upgrade))
            return;

        _hands.PickupOrDrop(args.User, upgrade, dropNear: true);

        _gun.RefreshModifiers(ent.Owner);

        _popup.PopupClient(
            Loc.GetString("gun-upgrade-removal-success"),
            ent.Owner,
            args.User);

        _adminLog.Add(
            LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.User):player} removed gun upgrade " +
            $"{ToPrettyString(upgrade)} from {ToPrettyString(ent.Owner)}.");
    }
}

[Serializable, NetSerializable]
public sealed partial class GunUpgradeRemovalFinishEvent : SimpleDoAfterEvent;
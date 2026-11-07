using Content.Server._Starlight.Achievement;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server.Destructible.Thresholds.Behaviors;

[UsedImplicitly]
[DataDefinition]
public sealed partial class BurnBodyBehavior : IThresholdBehavior
{
    /// <summary>
    ///     The popup displayed upon destruction.
    /// </summary>
    [DataField]
    public LocId PopupMessage = "bodyburn-text-others";

    public void Execute(EntityUid bodyId, DestructibleSystem system, EntityUid? cause = null)
    {
        var transformSystem = system.EntityManager.System<TransformSystem>();
        var inventorySystem = system.EntityManager.System<InventorySystem>();
        var sharedPopupSystem = system.EntityManager.System<SharedPopupSystem>();
        var adminLogger = IoCManager.Resolve<IAdminLogManager>();
        var achievementSystem = system.EntityManager.System<AchievementSystem>();

        if (system.EntityManager.TryGetComponent<InventoryComponent>(bodyId, out var comp))
        {
            foreach (var item in inventorySystem.GetHandOrInventoryEntities(bodyId))
            {
                transformSystem.DropNextTo(item, bodyId);
            }
        }

        var bodyIdentity = Identity.Entity(bodyId, system.EntityManager);
        sharedPopupSystem.PopupCoordinates(Loc.GetString(PopupMessage, ("name", bodyId)), transformSystem.GetMoverCoordinates(bodyId), PopupType.LargeCaution);
        //Starlight start - achievements and logger
        adminLogger.Add(LogType.Gib, LogImpact.High, $"Body of {bodyIdentity} was ashed by {cause}");
        achievementSystem.QueueUnlockAchievement(bodyId, "failed_phoenix");
        //Starlight stop

        system.EntityManager.QueueDeleteEntity(bodyId);
    }
}

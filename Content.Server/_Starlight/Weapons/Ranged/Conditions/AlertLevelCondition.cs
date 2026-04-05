using Content.Server.AlertLevel;
using Content.Server.Popups;
using Content.Shared._Starlight.StationGridMemory;
using Content.Shared.Station.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._Starlight.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;
using Robust.Shared.Serialization;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Weapons.Ranged.Conditions;

public sealed partial class AlertLevelCondition : FireModeCondition
{
    [DataField(required: true)]
    public List<string> AlertLevels;

    [DataField]
    public override string PopupMessage { get; set; } = "firemode-alert-level-condition";

    public override bool Condition(FireModeConditionConditionArgs args)
    {
        var entityManager = args.EntityManager;

        var alertSystem = entityManager.System<AlertLevelSystem>();

        var _popupSystem = entityManager.System<PopupSystem>();

        if (!entityManager.TryGetComponent<TransformComponent>(args.Shooter, out var transformComp)
            || !entityManager.TryGetComponent<ActorComponent>(args.Shooter, out var actor))
            return false;

        if (args.Weapon is null) return false; // realistically this should never ever ever be null why the fuck would this be null
        AlertLevelComponent? alertLevel = null;
        var allowed = false;
        if (entityManager.TryGetComponent<StationGridMemoryComponent>(args.Weapon.Value, out var stationMemory) &&
            entityManager.TryGetComponent<AlertLevelComponent>(stationMemory.LastStation, out alertLevel))
            allowed = CheckAlertLevel(stationMemory.LastStation, alertLevel, alertSystem);
        //failsafe
        else if (entityManager.TryGetComponent<StationMemberComponent>(transformComp.ParentUid, out var stationMember) &&
            entityManager.TryGetComponent<AlertLevelComponent>(stationMember.Station, out alertLevel))
            allowed = CheckAlertLevel(stationMember.Station, alertLevel, alertSystem);

        if(allowed) return true;
        _popupSystem.PopupEntity(Loc.GetString(PopupMessage), args.Shooter, actor.PlayerSession);
        return false;
    }

    private bool CheckAlertLevel(EntityUid station, AlertLevelComponent alertLevel, AlertLevelSystem alertSystem)
    {
        var currentAlertLevel = alertSystem.GetLevel(station, alertLevel);
        return AlertLevels.Contains(currentAlertLevel);
    }
}

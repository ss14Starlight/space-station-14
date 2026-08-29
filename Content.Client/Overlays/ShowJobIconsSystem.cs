using Content.Shared._Starlight.StatusIcon;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using Robust.Client.Player;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Medical.SuitSensor;

namespace Content.Client.Overlays;

public sealed partial class ShowJobIconsSystem : EquipmentHudSystem<ShowJobIconsComponent>
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;

    #region Starlight
    [Dependency] private StationAiVisionSystem _vision = default!;
    [Dependency] private IPlayerManager _player = default!;
    #endregion

    private static readonly ProtoId<JobIconPrototype> JobIconForNoId = "JobIconNoId";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusIconComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, StatusIconComponent _, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        var iconId = JobIconForNoId;

        // Starlight Start
        if (TryComp<FixedJobIconComponent>(uid, out var fixedIcon) && _prototype.Resolve(fixedIcon.Job, out var job))
        {
            iconId = job.Icon;
        }
        else if (_accessReader.FindAccessItemsInventory(uid, out var items))
        // Starlight End
        {
            foreach (var item in items)
            {
                // ID Card
                if (TryComp<IdCardComponent>(item, out var id))
                {
                    iconId = id.JobIcon;
                    break;
                }

                // PDA
                if (TryComp<PdaComponent>(item, out var pda)
                    && pda.ContainedId != null
                    && TryComp(pda.ContainedId, out id))
                {
                    iconId = id.JobIcon;
                    break;
                }
            }
        }

        // Starlight - start
        // Show job icons if entity is in camera view (only relevant for AI viewers) OR they have active suit sensors.

        // First, determine if the local viewer is an AI-style viewer. Only then consult the AI vision system.
        if (_player.LocalEntity is EntityUid localEnt
            && TryComp(localEnt, out StationAiOverlayComponent? _)
            && _vision.IsOutsideCameraViewCached(uid))
        {
            var suitSensorsActive = false;
            // Iterate all suit sensors and check if any are assigned to this user and active.
            foreach (var sensor in EntityQuery<SuitSensorComponent>(true))
            {
                if (sensor.User == uid && sensor.Mode == SuitSensorMode.SensorCords)
                {
                    suitSensorsActive = true;
                    break;
                }
            }

            if(!suitSensorsActive) return;
        }
        // Starlight - end

        if (_prototype.Resolve(iconId, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
        else
            Log.Error($"Invalid job icon prototype: {iconPrototype}");
    }
}

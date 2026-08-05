using Content.Server.Administration.Managers;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emag.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
// Starlight Start
using Content.Shared.Silicons.Borgs.Components;
using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Server.EUI;
using Robust.Server.Player;
using Content.Server._Starlight.Silicons;
using Content.Shared.Mind;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost;
// Starlight End

namespace Content.Server.Silicons.Borgs;

/// <inheritdoc/>
public sealed partial class BorgSystem : SharedBorgSystem
{
    [Dependency] private IBanManager _banManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    //Starlight Start
    [Dependency] private EuiManager _euiManager = null!;
    [Dependency] private IPlayerManager _playerManager = null!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private GhostSystem _ghostSystem = default!;
    //Starlight End

    public static readonly ProtoId<JobPrototype> BorgJobId = "Borg";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        // Starlight Start: Allow borgs to LOOC while crit
        SubscribeLocalEvent<BorgChassisComponent, LoocCritCheckEvent>(OnLoocCritCheckChassis);
        SubscribeLocalEvent<BorgBrainComponent, LoocCritCheckEvent>(OnLoocCritCheckBrain);
        SubscribeLocalEvent<BorgBrainComponent, AskBorgingChoiceEvent>(OnAskForBorging);
        // Starlight End

        InitializeTransponder();
    }

    public override bool CanPlayerBeBorged(ICommonSession session)
    {
        if (_banManager.GetJobBans(session.UserId)?.Contains(BorgJobId) == true)
            return false;

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateTransponder(frameTime);
    }

    // Starlight Start
    //Allow borgs to LOOC while crit
    private void OnLoocCritCheckChassis(EntityUid uid, BorgChassisComponent component, LoocCritCheckEvent args) =>
        args.AllowCritLooc = true;

    private void OnLoocCritCheckBrain(EntityUid uid, BorgBrainComponent component, LoocCritCheckEvent args) =>
        args.AllowCritLooc = true;

    private void OnAskForBorging(EntityUid uid, BorgBrainComponent component, AskBorgingChoiceEvent args)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        if (mind.UserId == null || !_playerManager.TryGetSessionById(mind.UserId.Value, out var client))
            return; // If we can't track down the client, we can't offer transfer. That'd be quite bad.
        _euiManager.OpenEui(new AcceptBorgingEui(uid, mindId, mind, this), client);

    }

    public void OpenGhostRole(EntityUid uid, EntityUid mindId, MindComponent mind)
    {
        if (!_ghostSystem.OnGhostAttempt(mindId, false)) //Set player as ghost, if this fails we leave them in there
            return;

        var ghostRole = EnsureComp<GhostRoleComponent>(uid);
        EnsureComp<GhostTakeoverAvailableComponent>(uid);

        ghostRole.RoleName = Loc.GetString("broken-borg-brain-role-name");
        ghostRole.RoleDescription = Loc.GetString("broken-borg-brain-role-description");
        ghostRole.RoleRules = Loc.GetString("ghost-role-information-silicon-rules");
        ghostRole.JobProto = BorgJobId;
        ghostRole.MindRoles = ["MindRoleGhostRoleSilicon"];
    }
    // Starlight End
}

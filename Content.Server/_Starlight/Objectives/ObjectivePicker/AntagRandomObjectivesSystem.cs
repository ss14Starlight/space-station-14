using System.Linq;
using Content.Server.Antag;
using Content.Server.Objectives;
using Content.Shared._Starlight.Mind.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Roles;
using Content.Shared.GameTicking;
using Content.Shared._Starlight.Objectives.Targeting;
using Content.Shared._Starlight.Traits.Antags;
using Content.Server.Revolutionary.Components;
using Content.Server._Starlight.Objectives.Components;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class AntagRandomObjectivesSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagRandomObjectivesComponent, AfterAntagEntitySelectedEvent>(CreatePicker,
            after: [typeof(AntagObjectivesSystem), typeof(Content.Server._Starlight.GameTicking.ChangelingRuleSystem)]);
        SubscribeNetworkEvent<ObjectivePickerSelected>(OnObjectivesSelected);
        SubscribeNetworkEvent<ObjectivePickerMulligan>(OnMulligan);
        SubscribeNetworkEvent<ObjectivePickerRequestAdditional>(OnRequestAdditionalObjectives);
        SubscribeNetworkEvent<ObjectivePickerSetOpen>(OnPickerSetOpen);
        SubscribeLocalEvent<PotentialObjectivesComponent, ComponentShutdown>(OnPickerShutdown);
        SubscribeLocalEvent<MindComponent, MindObjectivesChangedEvent>(OnObjectivesChanged);
        SubscribeLocalEvent<MindComponent, RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<MindComponent, RoleRemovedEvent>(OnRoleRemoved);
        SubscribeLocalEvent<MindComponent, MindGotAddedEvent>(OnMindAdded);
        SubscribeLocalEvent<MindComponent, MindGotRemovedEvent>(OnMindRemoved);
        SubscribeLocalEvent<MindContainerComponent, MobStateChangedEvent>(OnTargetMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeEligibilityChanges<MarkedForDeathComponent>();
        SubscribeEligibilityChanges<CommandStaffComponent>();
        SubscribeEligibilityChanges<NoObjectiveTargetComponent>();
        SubscribeEligibilityChanges<AntagTargetComponent>();
    }

    public void ApplySelectedObjectives(EntityUid mindId, IEnumerable<NetEntity> selectedObjectives) => TryApplySelectedObjectives(mindId, selectedObjectives.ToHashSet());
}

using System.Linq;
using Content.Server.Antag;
using Content.Server.Objectives;
using Content.Shared.Mind;
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
        SubscribeLocalEvent<PotentialObjectivesComponent, ComponentShutdown>(OnPickerShutdown);
    }

    public void ApplySelectedObjectives(EntityUid mindId, IEnumerable<NetEntity> selectedObjectives) => TryApplySelectedObjectives(mindId, selectedObjectives.ToHashSet());
}

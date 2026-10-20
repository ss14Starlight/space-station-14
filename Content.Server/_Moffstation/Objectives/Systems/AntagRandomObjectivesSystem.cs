using System.Linq;
using Content.Server._Moffstation.Objectives.Components;
using Content.Server.Antag;
using Content.Server.Objectives;
using Content.Shared._Moffstation.Objectives;
using Content.Shared.Mind;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Moffstation.Objectives.Systems;

public sealed partial class AntagRandomObjectivesSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagRandomObjectivesComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeNetworkEvent<ObjectivePickerSelected>(OnObjectivesSelected);
    }

    private void OnAntagSelected(Entity<AntagRandomObjectivesComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session == null)
            return;

        if (!_mind.TryGetMind(args.Session, out var mindId, out var mind))
        {
            Log.Error($"Antag {ToPrettyString(args.EntityUid):player} was selected by {ToPrettyString(ent):rule} but had no mind attached!");
            return;
        }

        if (!EnsureComp<PotentialObjectivesComponent>(mindId, out var potentialObjectives))
        {
            // Copying stuff over, probably a better way to do this but I am le tired
            potentialObjectives.MaxChoices = _random.Next(ent.Comp.MinChoices, ent.Comp.MaxChoices + 1);
            potentialObjectives.MinChoices = ent.Comp.MinChoices;
            potentialObjectives.AutoSelectionDelay = ent.Comp.SelectionDelay;
            // Starlight, MapInit has already used the default delay; apply the rule's configured delay.
            potentialObjectives.AutoSelectionTime = _timing.CurTime + ent.Comp.SelectionDelay;
        }

        #region Starlight
        // MaxOptions caps the entire offer, including options from earlier sets.
        foreach (var set in ent.Comp.Sets)
        {
            if (potentialObjectives.ObjectiveOptions.Count >= ent.Comp.MaxOptions)
                break;

            if (!_random.Prob(set.Prob))
                continue;

            foreach (var objective in _objectives.GetRandomObjectives(mindId, mind, set.Groups, float.MaxValue))
            {
                if (_objectives.GetInfo(objective, mindId, mind) is not { } info)
                {
                    TryQueueDel(objective);
                    continue;
                }

                potentialObjectives.ObjectiveOptions.Add(GetNetEntity(objective), info);

                // Stop before the generator creates another objective.
                if (potentialObjectives.ObjectiveOptions.Count >= ent.Comp.MaxOptions)
                    break;
            }
        }
        #endregion

        #region Starlight
        // Empty offers cannot be selected, and fewer offers reduce the selection limit.
        if (potentialObjectives.ObjectiveOptions.Count == 0)
        {
            RemCompDeferred<PotentialObjectivesComponent>(mindId);
            return;
        }

        potentialObjectives.MaxChoices = Math.Min(potentialObjectives.MaxChoices, potentialObjectives.ObjectiveOptions.Count);
        Dirty(mindId, potentialObjectives);
        #endregion
    }

    private void OnObjectivesSelected(ObjectivePickerSelected ev, EntitySessionEventArgs args)
    {
        #region Starlight
        // A player may only submit objectives for their own mind.
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _) || GetNetEntity(mindId) != ev.MindId)
            return;

        ApplySelectedObjectives(mindId, ev.SelectedObjectives);
        #endregion
    }

    public void ApplySelectedObjectives(EntityUid mindId, IEnumerable<NetEntity> selectedObjectives)
    {
        if (!TryComp<MindComponent>(mindId, out var mindComp))
            return;

        if (!TryComp<PotentialObjectivesComponent>(mindId, out var potentialObjectivesComp))
            return;

        #region Starlight
        // Validate the selection and dispose of every unselected offer.
        var selected = selectedObjectives.ToHashSet();
        if (selected.Count == 0 || selected.Count > potentialObjectivesComp.MaxChoices ||
            selected.Any(objective => !potentialObjectivesComp.ObjectiveOptions.ContainsKey(objective)))
            return;

        foreach (var objective in potentialObjectivesComp.ObjectiveOptions.Keys)
        {
            if (selected.Contains(objective))
            {
                _mind.AddObjective(mindId, mindComp, GetEntity(objective));
            }
            else
            {
                TryQueueDel(GetEntity(objective));
            }
        }
        // Deferred removal leaves the component present until the end of the tick.
        // Clearing its options prevents a second submission from adding duplicates.
        potentialObjectivesComp.ObjectiveOptions.Clear();
        Dirty(mindId, mindComp);
        RemCompDeferred<PotentialObjectivesComponent>(mindId);
        #endregion
    }
}

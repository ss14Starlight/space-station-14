using Content.Shared.Construction;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Tools.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.Tools.Systems;

public sealed partial class ToolRefinablSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedToolSystem _toolSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToolRefinableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ToolRefinableComponent, WelderRefineDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(EntityUid uid, ToolRefinableComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _toolSystem.UseTool(
            args.Used,
            args.User,
            uid,
            component.RefineTime,
            component.QualityNeeded,
            new WelderRefineDoAfterEvent(),
            fuel: component.RefineFuel);
    }

    private void OnDoAfter(EntityUid uid, ToolRefinableComponent component, WelderRefineDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (_net.IsClient)
            return;

        var component = ent.Comp;
        var uid = ent.Owner;

        var getIsBlocked = new AttemptToolRefineEvent(args.Used.Value);
        RaiseLocalEvent(args.Target.Value, ref getIsBlocked);
        if (getIsBlocked.IsCancelled)
        {
            _popup.PopupPredicted(getIsBlocked.BlockCause, args.User, args.User);
            return;
        }

        PopupOnRefine(ent, args.Used.Value, args.User);

        var ev = new BeforeToolRefinedEvent(args.User);
        RaiseLocalEvent(uid, ref ev);

        if (ev.Cancelled)
            return;

        if (component.RefineResult.Count == 0)
            Log.Warning($"Attempted to refine {ToPrettyString(ent)}, but no spawns were supplied. Refining should leave results.");
        else
        {
            // TODO: Use RandomPredicted https://github.com/space-wizards/RobustToolbox/pull/5849
            var rndSeed = SharedRandomExtensions.HashCodeCombine((int)_gameTiming.CurTick.Value, args.User.Id, uid.Id);
            var rng = new RobustRandom();
            rng.SetSeed(rndSeed);
            SpawnRefinement(component.RefineResult, uid, rng);
        }

        if (component.Sound != null)
            _audio.PlayPredicted(component.Sound, Transform(uid).Coordinates, args.User, AudioParams.Default.WithVolume(-2));

        _gib.Gib(uid);
        _destructible.DestroyEntity(uid);
    }

    private void SpawnRefinement(List<EntitySpawnEntry> spawnList, EntityUid source, IRobustRandom rng)
    {
        var spawns = EntitySpawnCollection.GetSpawns(spawnList, rng);
        var spawned = new List<EntityUid>(spawns.Count);

        if (_container.TryGetContainingContainer(source, out var container))
            _container.Remove((source, null, null), container);

        foreach (var protoId in spawns)
        {
            var refineResultUid = PredictedSpawnNextToOrDrop(protoId, source);
            spawned.Add(refineResultUid);

            if (container == null || !_container.Insert(refineResultUid, container))
            {
                var randVect = rng.NextVector2(2.0f, 2.5f);
                _physics.SetLinearVelocity(refineResultUid, randVect);
            }
        }

        if (!TryComp<ToolRefinableSolutionComponent>(source, out var comp))
            return;

        if (!TryGetSourceSolutionForTransfer(source, comp.SolutionToSplit, out var solutionInfo))
            return;

        var (sourceSoln, sourceSolution) = solutionInfo.Value;

        for (var i = spawned.Count; i > 0; i--)
        {
            var spawnedUid = spawned[i - 1];
            var refineResultVolume = sourceSolution.Volume / FixedPoint2.New(i);
            var lostSolution = _solutionContainer.SplitSolution(sourceSoln, refineResultVolume);
            FillResult(spawnedUid, comp.SolutionToSet, lostSolution);
        }
    }

    #endregion

    /// <summary>
    /// Show popup on finishing refining.
    /// </summary>
    /// <param name="ent">Entity that was refined.</param>
    /// <param name="used">Tool that was used to do refinement.</param>
    /// <param name="user">Entity that initiated refine process.</param>
    private void PopupOnRefine(Entity<ToolRefinableComponent> ent, EntityUid used, EntityUid user)
    {
        var component = ent.Comp;
        var uid = ent.Owner;

        var slicingDoneMessageForUser = component.PopupForUser == null
            ? null
            : Loc.GetString(component.PopupForUser, ("target", uid), ("tool", used));
        var slicingDoneMessageForOthers = component.PopupForOther == null
            ? null
            : Loc.GetString(component.PopupForOther, ("user", user), ("target", uid), ("tool", used));

        _popup.PopupPredicted(slicingDoneMessageForUser, slicingDoneMessageForOthers, user, user, component.PopupType);
    }

    /// <summary>
    /// Gets solution for transferring reagents into refine result from entity that was refined.
    /// </summary>
    /// <param name="source">Entity that is getting refined.</param>
    /// <param name="solutionName">Solution name (key) which should be used in process.</param>
    /// <param name="solutionInfo">
    /// Solution extra data that was extracted from <see cref="SolutionManagerComponent"/>
    /// (or null if there were no <see cref="SolutionManagerComponent"/> set or if there were
    /// no solution with name <see cref="solutionName"/>).
    /// </param>
    /// <returns></returns>
    private bool TryGetSourceSolutionForTransfer(
        EntityUid source,
        string solutionName,
        [NotNullWhen(true)] out (Entity<SolutionComponent> sourceSoln, Solution sourceSolution)? solutionInfo
    )
    {
        solutionInfo = default;

        if (!_solutionContainer.TryGetSolution(source, solutionName, out var sourceSoln, out var sourceSolution))
            return false;

        solutionInfo = (sourceSoln.Value, sourceSolution);
        return true;
    }

    /// <summary>
    /// Fills refine result entity with reagents from refined entity solution.
    /// </summary>
    /// <param name="refineResultUid">Entity to fill.</param>
    /// <param name="solutionName">Solution to which reagents should be added.</param>
    /// <param name="solution">Solution that should be used as source.</param>
    private void FillResult(EntityUid refineResultUid, string solutionName, Solution solution)
    {
        if (!_solutionContainer.TryGetSolution(refineResultUid, solutionName, out var itsSoln, out var itsSolution))
            return;

        _solutionContainer.RemoveAllSolution(itsSoln.Value);

        var lostSolutionPart = solution.SplitSolution(itsSolution.AvailableVolume);
        _solutionContainer.TryAddSolution(itsSoln.Value, lostSolutionPart);
    }

    private bool UseTool(EntityUid uid, ToolRefinableComponent component, EntityUid used, EntityUid user)
    {
        return _toolSystem.UseTool(
            used,
            user,
            uid,
            component.RefineTime,
            [component.QualityNeeded],
            new ToolRefineDoAfterEvent(),
            out _,
            fuel: component.RefineFuel
        );
    }
}

/// <summary>
/// Event for checking if tool refining of entity is blocked in some complex way.
/// </summary>
[ByRefEvent]
public record struct AttemptToolRefineEvent(
    EntityUid Using,
    bool IsCancelled = false,
    string? BlockCause = null
);

/// <summary>
/// Called after slicing of the entity.
/// </summary>
[ByRefEvent]
public record struct BeforeToolRefinedEvent(EntityUid User)
{
    public bool Cancelled;
}

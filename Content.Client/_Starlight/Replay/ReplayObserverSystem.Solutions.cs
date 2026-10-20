using System.Linq;
using Content.Client._Starlight.Replay.UI;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Replay;

// Read-only view of every solution on an entity, like chem goggles plus admin Edit Solutions. Solutions are networked
// to all clients, so recordings always have them. Windows refresh as the replay plays, one per entity.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private const float SolutionRefreshInterval = 0.2f;

    private readonly Dictionary<EntityUid, ReplaySolutionsWindow> _solutionWindows = new();
    private readonly List<EntityUid> _solutionScratch = new();
    private float _solutionRefreshAccumulator;

    private void AddSolutionsVerb(GetVerbsEvent<Verb> ev)
    {
        if (!HasComp<SolutionManagerComponent>(ev.Target) && !HasComp<SolutionComponent>(ev.Target))
            return;

        var target = ev.Target;
        ev.Verbs.Add(new Verb
        {
            Text = Loc.GetString("replay-observer-verb-view-solutions"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/drink.svg.192dpi.png")),
            Act = () => ShowSolutions(target),
            ClientExclusive = true,
            Priority = 9,
        });
    }

    private void ShowSolutions(EntityUid target)
    {
        if (_solutionWindows.TryGetValue(target, out var existing))
        {
            existing.MoveToFront();
            return;
        }

        var window = new ReplaySolutionsWindow { Title = Name(target) };
        window.OnClose += () => _solutionWindows.Remove(target);
        _solutionWindows[target] = window;
        window.SetContents(BuildSolutions(target));
        window.OpenCenteredLeft();
    }

    private void UpdateSolutionWindows(float frameTime)
    {
        if (_solutionWindows.Count == 0)
            return;

        _solutionRefreshAccumulator += frameTime;
        if (_solutionRefreshAccumulator < SolutionRefreshInterval)
            return;

        _solutionRefreshAccumulator = 0f;
        _solutionScratch.Clear();
        _solutionScratch.AddRange(_solutionWindows.Keys);
        foreach (var target in _solutionScratch)
        {
            if (!_solutionWindows.TryGetValue(target, out var window))
                continue;

            // Deleted, or skipped past its existence: close rather than show stale contents.
            if (!Exists(target))
            {
                window.Close();
                continue;
            }

            window.SetContents(BuildSolutions(target));
        }
    }

    private void CloseSolutionWindows()
    {
        _solutionScratch.Clear();
        _solutionScratch.AddRange(_solutionWindows.Keys);
        foreach (var target in _solutionScratch)
        {
            if (_solutionWindows.TryGetValue(target, out var window))
                window.Close();
        }

        _solutionWindows.Clear();
        _solutionRefreshAccumulator = 0f;
    }

    private FormattedMessage BuildSolutions(EntityUid target)
    {
        var msg = new FormattedMessage();
        foreach (var (name, solution) in _solutions.EnumerateSolutions(new Entity<SolutionManagerComponent?>(target, null)))
        {
            if (!msg.IsEmpty)
                msg.PushNewline();

            AppendSolution(msg, name ?? SolutionComponent.DefaultSolutionId, solution.Comp.Solution);
        }

        if (msg.IsEmpty)
            msg.AddMarkupOrThrow(Loc.GetString("replay-observer-solutions-none"));

        return msg;
    }

    private void AppendSolution(FormattedMessage msg, string name, Solution solution)
    {
        msg.AddMarkupOrThrow(Loc.GetString("replay-observer-solution-header",
            ("name", name),
            ("volume", solution.Volume),
            ("max", solution.MaxVolume)));

        var reagents = solution.GetReagentPrototypes(_proto)
            .OrderByDescending(pair => pair.Value.Value)
            .ThenBy(pair => pair.Key.LocalizedName);

        foreach (var (proto, quantity) in reagents)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("scannable-solution-chemical",
                ("type", proto.LocalizedName),
                ("color", proto.SubstanceColor.ToHexNoAlpha()),
                ("amount", quantity)));
        }

        if (solution.Volume > 0)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("scannable-solution-temperature",
                ("temperature", Math.Round(solution.Temperature))));
        }
    }
}

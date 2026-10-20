using System.Linq;
using Content.Client.Examine;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Replay;

// Read-only view of every solution on an entity, like chem goggles plus admin Edit Solutions. Solutions are networked
// to all clients, so recordings always have them.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ExamineSystem _examine = default!;

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
        if (_observer is not { } observer)
            return;

        var msg = new FormattedMessage();
        foreach (var (name, solution) in _solutions.EnumerateSolutions(new Entity<SolutionManagerComponent?>(target, null)))
        {
            if (!msg.IsEmpty)
                msg.PushNewline();

            AppendSolution(msg, name ?? SolutionComponent.DefaultSolutionId, solution.Comp.Solution);
        }

        if (msg.IsEmpty)
            msg.AddMarkupOrThrow(Loc.GetString("replay-observer-solutions-none"));

        _examine.OpenTooltip(observer, target, out _, out _, examiner: observer);
        _examine.UpdateTooltipInfo(observer, target, msg, getVerbs: false);
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

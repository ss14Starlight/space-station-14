
using Content.Shared.Humanoid;
using Content.Shared.GameTicking;
using Robust.Shared.Random;
using Content.Shared.Paper;
using Content.Shared.Item;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.StoryGen;
public sealed partial class SharedGrudgeSystem : EntitySystem
{
    [Dependency] private StoryGenLocalizationManager _sglm = default!;
    [Dependency] private IEntityManager _entManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrudgeBookComponent, ComponentStartup>(OnGrudgeBookSpawn);
        SubscribeLocalEvent<GrudgeBookComponent, ComponentShutdown>(OnGrudgeBookDestroyed);
        SubscribeLocalEvent<GrudgeBearerComponent, ComponentShutdown>(OnGrudgeBearerDestroyed);

        SubscribeLocalEvent<GrudgeBearerComponent, PlayerSpawnCompleteEvent>(OnNewSubjectEncountered);
    }
    private void OnNewSubjectEncountered(Entity<GrudgeBearerComponent> entity, ref PlayerSpawnCompleteEvent ev)
    {
        if(HasComp<HumanoidAppearanceComponent>(ev.Mob)) {
            if(GenerateGrudge(entity.AsType(), ev.Mob)) {
                // play update sound
            }
        }
    }

    public bool GenerateGrudge(EntityUid bearer, EntityUid target)
    {
        if (!TryComp<GrudgeBearerComponent>(bearer, out var bearerComp))
            return false;

        if (!TryComp<GrudgeBookComponent>(bearerComp.Book, out var bookComp))
            return false;

        if (bookComp.rng.NextFloat() > bearerComp.judginess)
            return false;
        else
        {
            // add grudge

            if (!TryComp<PaperComponent>(bearerComp.Book, out var paperComp))
                return false;

            var template = bookComp.dataset;

            // TODO: retrieve specific template from dataset

            paperComp.Content = paperComp.Content + "\n\n" + Loc.GetString(template,
                ("owner", bearer),
                ("ownerAncestor", "RELATIVE OF SOME SORT"),
                ("target", target),
                ("targetAncestor", "RELATIVE OF SOME SORT"));

            return true;
        }
    }

    private void OnGrudgeBookSpawn(Entity<GrudgeBookComponent> entity, ref ComponentStartup ev)
    {
        entity.Comp.rng = new RobustRandom();

        if (!TryComp<ItemComponent>(entity, out var itemComp))
            return;

        // identify owner
        EntityUid owner = entity; // FIXME

        // write preamble
        if (!TryComp<PaperComponent>(entity, out var paperComp))
            return;

        paperComp.Content = Loc.GetString(entity.Comp.preamble, ("owner", (object)owner));

        // iterate over crew
        var hits = 0;
        if (false) { // TODO: iterate over crew
            var target = entity; // TODO
            if(HasComp<HumanoidAppearanceComponent>(target)) {
                if(GenerateGrudge(entity.AsType(), target))
                    hits += 1;
            }
        }

        if (hits > 0) {
            // TODO: play update sound
        }
    }

    private void OnGrudgeBookDestroyed(Entity<GrudgeBookComponent> entity, ref ComponentShutdown ev)
    {
        // i am not sure this is even necessary
        if (!TryComp<GrudgeBearerComponent>(entity.Comp.RightfulOwner, out var bearerComp))
            return;

        bearerComp.Book = null;
    }

    private void OnGrudgeBearerDestroyed(Entity<GrudgeBearerComponent> entity, ref ComponentShutdown ev)
    {
        // i am not sure this is even necessary
        if (!TryComp<GrudgeBookComponent>(entity.Comp.Book, out var bookComp))
            return;

        bookComp.RightfulOwner = null;
    }
}

namespace Content.Shared._Starlight.Genetics.GeneticTraits.Parts;

/*
 * Okay, so why does this exist?
 * During development of genetics, I reworked genes so that they would be more generic. A trait should be anything that
 * is handled by a system that takes a UpdateTraitComponentsEvent. Surely this would include an associated component
 * but that's not necessary. Important to this is that GenesSystem should be blind. The previous implementation
 * was very concerned with a trait's "nature", i.e. whether it was passive, occurred once, or was based on a chemical
 * solution, so that the trait could be handed off to the correct system. But that made things highly ungeneric. So,
 * now the GenesSystem is blind. But now it's the responsibility of some other system(s). So where does it lay?
 * I considered first handing it off to the trait systems. After all, the systems manage the components, surely they
 * should have primacy. The issue is that in order to let systems "be aware" of the relevant traits, they will need
 * to receive a local event attached to GenesComponent, specifically of the form:
 * SubscribeLocalEvent<GenesComponent, UpdateTraitComponentsEvent>(...);
 * But only one such subscription can exist at a time. This is very much not compatible with multiple systems, which is
 * the entire point of this rework. I think this is born from Events being a way for system A to talk to system B and
 * vise versa, not so much a way for systems B-Z to subscribe to system A. If there is a subscriber model somewhere,
 * please let me know.
 * In any case, the next best option was to leave it up to the components themselves. This is practically the same as
 * putting it in the system with the cost that it's outside the system source file. This may add a bit more flexibility
 * too but I don't know.
 * I feel like there's a more elegant solution somewhere and I just can't think of it. Ah well, this works. This might
 * even be the most elegant solution and I'm pained by how simple it comes across.
 */

/// <summary>
/// All trait prototypes should inherit this interface. Defines how to set up an entity to work with a trait system.
/// </summary>
public interface IGeneticTraitSetup
{
    /// <summary>
    /// This modifies the entity to "set it up" for a relevant system. Typically, you'll want to ensure the relevant
    /// component, but this can do anything necessary. Default behavior is to do nothing.
    /// </summary>
    /// <param name="entityManager">The entity manager, usually obtained through IoC.</param>
    /// <param name="entityUid">The entity that needs setting up for a trait.</param>
    void GeneticTraitSetup(EntityManager entityManager, EntityUid entityUid) { }
}


using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.DragDrop;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.StepTrigger.Systems;

namespace Content.Server._Starlight.Medical.Virology;

/// <summary>
/// Turns physical contact with a contamination source into bacterial exposure.
/// </summary>
/// <remarks>
/// Bacteria used to reach people through the same proximity sweep as fungus, which meant
/// a blood puddle could infect someone who never touched it. That undercut the one thing
/// separating bacteria from an airborne threat, so the sweep now carries fungus only and
/// every bacterial route goes through here.
///
/// Person-to-person contact stays in <see cref="PathogenContactSystem"/>; this system is
/// only ever environment-to-person, so the hooks below are deliberately about handling
/// filth rather than touching people.
/// </remarks>
public sealed partial class PathogenSourceContactSystem : EntitySystem
{
    [Dependency] private PathogenContaminationSourceSystem _sources = default!;
    [Dependency] private PathogenTransmissionSystem _transmission = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Interaction raises these with the broadcast flag set, so a plain subscription
        // sees every one. Handling filth with bare hands, or with a tool - butchering a
        // rotting body, prying at rotten food.
        SubscribeLocalEvent<InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing);

        // These three are directed-only, so they need a component to hang off. Sources
        // have nothing in common but a transform - a puddle, a corpse and a bag of
        // organic waste share no other component - and the lookup below misses cheaply
        // for the overwhelming majority of entities that are not sources.
        SubscribeLocalEvent<TransformComponent, StepTriggeredOffEvent>(OnStepped);
        SubscribeLocalEvent<TransformComponent, GotEquippedHandEvent>(OnPickedUp);
        SubscribeLocalEvent<TransformComponent, DragDropTargetEvent>(OnDragDrop);

        // Dragging a corpse to medbay is how bodies actually get handled, and pulling
        // already raises this for person-to-person contact.
        SubscribeLocalEvent<PathogenContactEvent>(OnContact);
    }

    private void OnInteractHand(InteractHandEvent args)
        => TryExpose(args.Target, args.User);

    private void OnInteractUsing(InteractUsingEvent args)
        => TryExpose(args.Target, args.User);

    private void OnStepped(Entity<TransformComponent> entity, ref StepTriggeredOffEvent args)
        => TryExpose(args.Source, args.Tripper);

    private void OnPickedUp(Entity<TransformComponent> entity, ref GotEquippedHandEvent args)
        => TryExpose(args.Equipped, args.User);

    /// <summary>
    /// Raised on the container being dropped onto, so the body is the dragged entity
    /// rather than the subscriber. This is the bagging case.
    /// </summary>
    private void OnDragDrop(Entity<TransformComponent> entity, ref DragDropTargetEvent args)
        => TryExpose(args.Dragged, args.User);

    private void OnContact(PathogenContactEvent args)
    {
        TryExpose(args.First, args.Second);
        TryExpose(args.Second, args.First);
    }

    /// <summary>
    /// Exposes <paramref name="target"/> to whatever bacteria <paramref name="source"/>
    /// is breeding. Isolation and prevalence caps are enforced upstream, so a bagged or
    /// morgued body cannot infect its handler.
    /// </summary>
    private bool TryExpose(EntityUid source, EntityUid target)
    {
        if (source == target ||
            !_sources.TryGetContactExposure(source, out var strains, out var chance))
        {
            return false;
        }

        foreach (var strain in strains)
        {
            if (_transmission.TryExpose(target, strain, chance))
                return true;
        }

        return false;
    }
}

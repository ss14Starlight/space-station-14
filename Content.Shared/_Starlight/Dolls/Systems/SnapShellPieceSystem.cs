using System.Linq;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Dolls.Events;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Starlight.Dolls.Systems;

public sealed partial class SnapShellPieceSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SnapShellPieceEvent>(OnSnapShellPiece);
    }

    private void OnSnapShellPiece(SnapShellPieceEvent ev)
    {
        if (ev.Handled)
            return;
        var user = ev.Performer;

        //Find shell piece to drop
        var allShellPieces = _body.GetBodyOrgans(user).Where(o => TryComp(o.Id, out OrganShellComponent? _));
        var shellPieces = allShellPieces.ToList();
        if (shellPieces.Count == 0)
            return; //No shell pieces to drop

        var droppedEntity = _random.Pick(shellPieces); //Randomise!

        //Drop piece on the ground *unless* we require a free hand.
        if (ev.RequiresFreeHand && !_hands.CanPickupAnyHand(user, droppedEntity.Id))
            return; //If we can't pick up the shell piece, but have to, we don't try to drop it.

        var part = _body.GetParentPartOrNull(droppedEntity.Id); //Need to determine part while it's still attached

        if (!_container.TryRemoveFromContainer(droppedEntity.Id))
            return; //Failsafe if the shell piece cannot be dropped for some reason.

        if (part != null) //If it was attached to the body, which it always should, but just in case, we raise the surgery event on it
        {
            var sev = new SurgeryOrganExtracted(user, part.Value, droppedEntity.Id);
            _entityManager.EventBus.RaiseLocalEvent(droppedEntity.Id, ref sev);
        }

        if(ev.RequiresFreeHand)
            if (!_hands.TryPickupAnyHand(user, droppedEntity.Id))
                return; //Final failsafe if picking up the piece fails.

        if(ev.SelfDamage != null) //If we have damage to apply, do so
            _damageable.ChangeDamage(user, ev.SelfDamage, true);

        ev.Handled = true; //Done!
    }
}

/// <summary>
/// Regrows a piece of the shell, with the given prototype
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class RegrowShellEntityEffectSystem : EntityEffectSystem<ShellComponent, RegrowShell>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected override void Effect(Entity<ShellComponent> entity, ref EntityEffectEvent<RegrowShell> args)
    {
        //Find empty shell slots
        var emptySlots = GetShellContainers(entity.Owner).ToList();

        IEnumerable<BaseContainer> GetShellContainers(EntityUid uid)
        {
            var bodyParts = _body.GetBodyChildren(uid).ToList();
            foreach (var child in bodyParts)
            {
                if (!_container.TryGetContainer(child.Id, SharedBodySystem.GetOrganContainerId("shell"), out var shellContainer))
                    continue;
                if (shellContainer.Count == 0)
                    yield return shellContainer;
            }
        }

        if (emptySlots.Count == 0)
            return; //No need to regrow anything


        _random.Shuffle(emptySlots); //Randomise!
        var regrownSlot = emptySlots.First();
        var regrownShell = Spawn(args.Effect.ShellProto);

        if(!_container.CanInsert(regrownShell, regrownSlot))
            QueueDel(regrownShell); //If we can't insert the shell piece for whatever reason, delete it
        _container.Insert(regrownShell, regrownSlot);

        var part = _body.GetParentPartOrNull(regrownShell);
        if(part == null)
            QueueDel(regrownShell); //If insertion somehow didn't put the shell piece into a valid body part, delete it
        else
        {
            var sev = new SurgeryOrganImplantationCompleted(entity.Owner, part.Value, regrownShell);
            _entityManager.EventBus.RaiseLocalEvent(regrownShell, ref sev);
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class RegrowShell : EntityEffectBase<RegrowShell>
{
    /// <summary>
    /// Which shell prototype are we regrowing?
    /// </summary>
    [DataField]
    public EntProtoId ShellProto = "ShellFragmentDoll";

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc) // Starlight
        =>
            loc.GetString("entity-effect-guidebook-regrow-doll-shell", ("chance", Probability));
}

using System.Linq;
using Content.Shared.Actions;
using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Alert;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Dolls;

public sealed partial class ShellSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShellComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShellComponent, ComponentShutdown>(OnCompRemove);
        SubscribeLocalEvent<ShellComponent, DamageChangedEvent>(OnDamaged);
    }

    private void OnMapInit(EntityUid uid, ShellComponent comp, MapInitEvent args)
    {
        _actionsSystem.AddAction(uid, ref comp.GenerateShellPieceActionEntity, comp.GenerateShellPieceAction);
        _alerts.ShowAlert(uid, comp.ShellAlert);
    }

    private void OnCompRemove(EntityUid uid, ShellComponent comp, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, comp.GenerateShellPieceActionEntity);
        _alerts.ClearAlert(uid, comp.ShellAlert);
    }

    private void OnDamaged(EntityUid uid, ShellComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == uid)
            return; //No need to do anything if we are only healing or are the origin of the damage.

        if (args.DamageDelta == null)
            return; //Don't apply if we set the damage directly please.

        var group = _prototypeManager.Index<DamageGroupPrototype>(comp.DestroyedBy);
        if (!args.DamageDelta.TryGetDamageInGroup(group, out var damageTaken))
            return; //If we have no damage of the cracking type, do nothing.

        if (damageTaken - comp.Hardness <= 0)
            return; //Shell too strong, no breakage

        if (comp.Stability != 0 //if stability is 0, what are we even doing computing a chance?
            && _random.Prob(Math.Clamp(damageTaken.Float() / comp.Stability, 0f, 100f) * 0.01f))
        {
            //Break the shell! 100 brute damage (after scaling by stability) at once guarantees a break.

            //Find shell piece to destroy
            var allShellPieces = _body.GetBodyOrgans(uid).Where(o => TryComp(o.Id, out OrganShellComponent? _));
            var shellPieces = allShellPieces.ToList();
            if (shellPieces.Count == 0)
                return; //No shell pieces to destroy

            var entityToDestroy = _random.Pick(shellPieces);//Randomise!

            var part = _body.GetParentPartOrNull(entityToDestroy.Id); //Need to determine part while it's still attached

            if (!_container.TryRemoveFromContainer(entityToDestroy.Id))
                return; //Failsafe if the shell piece cannot be dropped for some reason.

            if (part != null) //If it was attached to the body, which it always should, but just in case, we raise the surgery event on it
            {
                var sev = new SurgeryOrganExtracted(uid, part.Value, entityToDestroy.Id);
                _entityManager.EventBus.RaiseLocalEvent(entityToDestroy.Id, ref sev);
            }
            _audioSystem.PlayPredicted(comp.ShellBreakSound, uid, uid);
            QueueDel(entityToDestroy.Id); //Destroy it
        }
    }
}

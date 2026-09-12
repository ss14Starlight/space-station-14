using System.Linq;
using Content.Shared.Intellicard;
using Content.Shared.Mind;
using Content.Shared.PAI;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
//ReSharper disable CheckNamespace
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared._Starlight.Silicons.Borgs;

namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    private void OnIntellicardPaiDoAfter(Entity<PAIComponent> ent, ref IntellicardDoAfterEvent args) => TransferPai(ent.Owner, args);

    // Starlight: wipe the name of the entity to the prototype name, used for entities when they are downloaded/uploaded
    private void ResetNameToPrototype(EntityUid entity)
    {
        if (MetaData(entity).EntityPrototype is { } prototype)
            _metadata.SetEntityName(entity, prototype.Name);
    }

    private bool IsAiInterface(EntityUid entity)
    {
        if (TryComp<BorgChassisComponent>(entity, out var chassis) && chassis.BrainEntity is { } brain)
            entity = brain;

        return HasComp<StationAIShuntComponent>(entity);
    }

    private void TransferPai(EntityUid pai, IntellicardDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Args.Target is not { } card || !TryComp<StationAiHolderComponent>(card, out var cardHolder))
            return;

        var slot = cardHolder.Slot;
        if (slot.Item is { } stored)
        {
            if (!_mind.TryGetMind(stored, out var mindId, out var mind))
                return;

            if (_mind.TryGetMind(pai, out _, out _))
            {
                _popup.PopupEntity(Loc.GetString("intellicard-core-occupied"), pai, args.User, PopupType.Large);
                return;
            }

            var name = _nameModifier.GetBaseName(stored);
            _mind.TransferTo(mindId, pai, ghostCheckOverride: true, mind: mind);
            _metadata.SetEntityName(pai, name);
            Del(stored);
            args.Handled = true;
            return;
        }

        if (slot.ContainerSlot is not { } containerSlot || !_mind.TryGetMind(pai, out var paiMindId, out var paiMind))
            return;

        var paiName = _nameModifier.GetBaseName(pai);
        var brain = SpawnInContainerOrDrop(DefaultAi, card, containerSlot.ID);
        _metadata.SetEntityName(brain, paiName);
        _metadata.SetEntityName(card, paiName);
        _mind.TransferTo(paiMindId, brain, ghostCheckOverride: true, mind: paiMind);
        ResetNameToPrototype(pai);
        args.Handled = true;
    }

    public bool TryControlAI(EntityUid mindId, EntityUid uid)
    {
        if (!HasComp<StationAiCoreComponent>(uid)) return false;
        foreach (var entity in _containers.GetAllContainers(uid).SelectMany(container => container.ContainedEntities))
        {
            if (!HasComp<BorgBrainComponent>(entity)) continue;
            _mind.TransferTo(mindId, entity);
            return true;
        }
        var brain = SpawnInContainerOrDrop("StationAiBrainConstructed", uid, StationAiCoreComponent.Container);
        _mind.TransferTo(mindId, brain);
        return true;
    }
}

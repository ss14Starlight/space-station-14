using System.Linq;
using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Interaction;
using Content.Shared.Radio.Components;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._FarHorizons.Silicons.IPC;

public abstract partial class SharedIPCSystem 
{
    protected virtual void SetupRadio()
    {
        SubscribeLocalEvent<IPCRadioComponent, ComponentStartup>(OnRadioStartup);
        SubscribeLocalEvent<IPCRadioComponent, InteractUsingEvent>(OnRadioInteractUsing);
    }

    public bool InsertEncryptionKey(EntityUid target, EntityUid user, EntityUid key)
    {
        if (!TryComp<IPCRadioComponent>(target, out var radio))
            return false;

        if (!TryComp<WiresPanelComponent>(target, out var panel) || !panel.Open)
        {
            _popup.PopupPredicted(Loc.GetString("encryption-keys-panel-locked"), target, user);
            return false;
        }

        if (radio.EncryptionKeysContainer.ContainedEntities.Count >= radio.KeysCapacity)
        {
            _popup.PopupPredicted(Loc.GetString("encryption-key-slots-already-full"), target, user);
            return false;
        }

        if (_container.Insert(key, radio.EncryptionKeysContainer))
        {
            _popup.PopupPredicted(Loc.GetString("encryption-key-successfully-installed"), target, user);
            _audio.PlayPredicted(radio.KeyInsertionSound, target, user);
            return true;
        }
        return false;
    }
    
    public void EjectEncryptionKeys(EntityUid target, EntityUid user){
        if (!TryComp<IPCRadioComponent>(target, out var radio))
            return;

        var contained = radio.EncryptionKeysContainer.ContainedEntities.ToArray();
        _container.EmptyContainer(radio.EncryptionKeysContainer, reparent: false);
        foreach (var ent in contained)
        {
            _hands.PickupOrDrop(user, ent, dropNear: true);
        }

        _popup.PopupPredicted(Loc.GetString("encryption-keys-all-extracted"), target, user);
        _audio.PlayPredicted(radio.KeyExtractionSound, target, user);
    }

    protected void OnRadioStartup(Entity<IPCRadioComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.EncryptionKeysContainer = _container.EnsureContainer<Container>(ent, ent.Comp.EncryptionKeysContainerID);
        ent.Comp.RadioTransmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(ent);
        ent.Comp.RadioReceiver = EnsureComp<ActiveRadioComponent>(ent);

    }

    private void OnRadioInteractUsing(Entity<IPCRadioComponent> uid, ref InteractUsingEvent args){
        if (args.Handled)
            return;

        if (HasComp<EncryptionKeyComponent>(args.Used)){
            InsertEncryptionKey(args.Target, args.User, args.Used);
            args.Handled = true;
        }
    }
}
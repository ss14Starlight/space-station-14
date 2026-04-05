using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Body.Events;
using Content.Shared.Gibbing;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Containers;

namespace Content.Server._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    protected override void SetupRadio()
    {
        base.SetupRadio();

        SubscribeLocalEvent<IPCRadioComponent, StartingGearEquippedEvent>(OnRadioStartingGear);
        SubscribeLocalEvent<IPCRadioComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<IPCRadioComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<IPCRadioComponent, BeingGibbedEvent>(OnRadioGibbed);
    }

    private void OnRadioGibbed(Entity<IPCRadioComponent> ent, ref BeingGibbedEvent args) =>
        _container.EmptyContainer(ent.Comp.EncryptionKeysContainer);
    public void UpdateRadioChannels(Entity<IPCRadioComponent> ent)
    {
        if (!ent.Comp.RadioTransmitter.Initialized)
            return;

        ent.Comp.RadioTransmitter.Channels.Clear();
        ent.Comp.RadioReceiver.Channels.Clear();

        foreach (var key in ent.Comp.EncryptionKeysContainer.ContainedEntities)
            if (TryComp<EncryptionKeyComponent>(key, out var keyComp)){
                ent.Comp.RadioTransmitter.Channels.UnionWith(keyComp.Channels);
                ent.Comp.RadioReceiver.Channels.UnionWith(keyComp.Channels);
            }
    }

    private void OnRadioStartingGear(Entity<IPCRadioComponent> ent, ref StartingGearEquippedEvent args)
    {
        if (ent.Comp.CopyHeadsetKeys)
            CopyHeadsetKeys(ent);

        if (ent.Comp.RemoveHeadsetOnRoundstart)
            RemoveHeadset(ent);
    }

    private void OnContainerChanged(Entity<IPCRadioComponent> uid, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == uid.Comp.EncryptionKeysContainerID)
            UpdateRadioChannels(uid);
    }

    private void OnContainerChanged(Entity<IPCRadioComponent> uid, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == uid.Comp.EncryptionKeysContainerID)
            UpdateRadioChannels(uid);
    }

    private void CopyHeadsetKeys(Entity<IPCRadioComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.HeadsetContainerID, out var ear) ||
            ear.ContainedEntities.Count != 1)
            return;

        if (!_container.TryGetContainer(ear.ContainedEntities[0], ent.Comp.EncryptionKeysContainerID, out var headset))
            return;

        foreach (var item in headset.ContainedEntities){
            if (!TryComp<EncryptionKeyComponent>(item, out var key))
                continue;

            SpawnInContainerOrDrop(Prototype(item)?.ID, ent, ent.Comp.EncryptionKeysContainerID);
        }
    }

    private void RemoveHeadset(Entity<IPCRadioComponent> ent){
        if (!_container.TryGetContainer(ent, ent.Comp.HeadsetContainerID, out var ear) ||
            ear.ContainedEntities.Count != 1)
            return;

        Del(ear.ContainedEntities[0]);
    }

    private void AddRadioVerbs(GetVerbsEvent<Verb> ev){
        if (!ev.CanInteract || !ev.CanAccess ||
            !TryComp<IPCRadioComponent>(ev.Target, out var radio) ||
            radio.EncryptionKeysContainer.ContainedEntities.Count < 1 ||
            !TryComp<WiresPanelComponent>(ev.Target, out var wires) ||
            !wires.Open)
            return;

        var verb = new Verb
        {
            Text = "Encryption keys",
            Category = VerbCategory.Eject,
            IconEntity = GetNetEntity(radio.EncryptionKeysContainer.ContainedEntities[0]),
            Act = () => EjectEncryptionKeys(ev.Target, ev.User),
        };

        ev.Verbs.Add(verb);
    }
}

using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Radio.Components;
using System.Linq; // Starlight

namespace Content.Server.Implants;

public sealed class RadioImplantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<RadioImplantComponent, ImplantRemovedEvent>(OnImplantRemoved);
    }

    /// <summary>
    /// If implanted with a radio implant, installs the necessary intrinsic radio components
    /// </summary>
    private void OnImplantImplanted(Entity<RadioImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        var activeRadio = EnsureComp<ActiveRadioComponent>(args.Implanted);
        //Starlight begin
        foreach (var channel in ent.Comp.RadioChannels.Where(channel => activeRadio.Channels.Add(channel)))
            ent.Comp.ActiveAddedChannels.Add(channel);

        foreach (var channel in ent.Comp.CustomChannels.Where(channel => activeRadio.CustomChannels.Add(channel)))
            ent.Comp.ActiveAddedCustomRadioChannels.Add(channel);
        Dirty(args.Implanted, activeRadio);
        //Starlight end

        EnsureComp<IntrinsicRadioReceiverComponent>(args.Implanted);

        var intrinsicRadioTransmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(args.Implanted);

        //Starlight begin
        foreach (var channel in
                 ent.Comp.RadioChannels.Where(channel => intrinsicRadioTransmitter.Channels.Add(channel)))
            ent.Comp.TransmitterAddedChannels.Add(channel);

        foreach (var channel in ent.Comp.CustomChannels.Where(channel =>
                     intrinsicRadioTransmitter.CustomChannels.Add(channel)))
            ent.Comp.TransmitterAddedCustomRadioChannels.Add(channel);
        Dirty(args.Implanted, intrinsicRadioTransmitter);
        //Starlight end
    }

    /// <summary>
    /// Removes intrinsic radio components once the Radio Implant is removed
    /// </summary>
    private void OnImplantRemoved(Entity<RadioImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        if (TryComp<ActiveRadioComponent>(args.Implanted, out var activeRadioComponent))
        {
            foreach (var channel in ent.Comp.ActiveAddedChannels)
            {
                activeRadioComponent.Channels.Remove(channel);
            }
            ent.Comp.ActiveAddedChannels.Clear();
            //Starlight begin
            foreach (var channel in ent.Comp.ActiveAddedCustomRadioChannels)
                activeRadioComponent.CustomChannels.Remove(channel);
            ent.Comp.ActiveAddedCustomRadioChannels.Clear();
            //Starlight end

            if (activeRadioComponent.Channels.Count == 0 && activeRadioComponent.CustomChannels.Count == 0) // Starlight edit
            {
                RemCompDeferred<ActiveRadioComponent>(args.Implanted);
            }

            Dirty(args.Implanted, activeRadioComponent); // Starlight
        }

        if (!TryComp<IntrinsicRadioTransmitterComponent>(args.Implanted, out var radioTransmitterComponent))
            return;

        foreach (var channel in ent.Comp.TransmitterAddedChannels)
        {
            radioTransmitterComponent.Channels.Remove(channel);
        }
        Dirty(args.Implanted, radioTransmitterComponent); //Starlight
        ent.Comp.TransmitterAddedChannels.Clear();

        //Starlight begin
        foreach (var channel in ent.Comp.TransmitterAddedCustomRadioChannels)
            radioTransmitterComponent.CustomChannels.Remove(channel);
        ent.Comp.TransmitterAddedCustomRadioChannels.Clear();
        //Starlight end

        if ((radioTransmitterComponent.Channels.Count == 0 || activeRadioComponent?.Channels.Count == 0) && (radioTransmitterComponent.CustomChannels.Count==0 || activeRadioComponent?.CustomChannels.Count == 0)) // Starlight edit
        {
            RemCompDeferred<IntrinsicRadioTransmitterComponent>(args.Implanted);
        }
    }
}

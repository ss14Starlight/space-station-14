using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Radio.Components;

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
        foreach (var channel in ent.Comp.RadioChannels)
        {
            if (activeRadio.Channels.Add(channel))
                ent.Comp.ActiveAddedChannels.Add(channel);
        }
        //Starlight begin
        foreach (var channel in ent.Comp.CustomChannels)
            if (activeRadio.CustomChannels.Add(channel))
                ent.Comp.ActiveAddedCustomRadioChannels.Add(channel);
        //Starlight end

        EnsureComp<IntrinsicRadioReceiverComponent>(args.Implanted);

        var intrinsicRadioTransmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(args.Implanted);
        foreach (var channel in ent.Comp.RadioChannels)
        {
            if (intrinsicRadioTransmitter.Channels.Add(channel))
                ent.Comp.TransmitterAddedChannels.Add(channel);
        }
        //Starlight begin
        foreach (var channel in ent.Comp.CustomChannels)
            if (intrinsicRadioTransmitter.CustomChannels.Add(channel))
                ent.Comp.TransmitterAddedCustomRadioChannels.Add(channel);
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
        }

        if (!TryComp<IntrinsicRadioTransmitterComponent>(args.Implanted, out var radioTransmitterComponent))
            return;

        foreach (var channel in ent.Comp.TransmitterAddedChannels)
        {
            radioTransmitterComponent.Channels.Remove(channel);
        }
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

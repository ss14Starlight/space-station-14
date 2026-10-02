using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared._Starlight.StationRadio.Events;
using Content.Shared.Trigger;

namespace Content.Shared._Starlight.StationRadio.Systems;


public sealed partial class TriggerOnVinylFinishedSystem : TriggerOnXSystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<TriggerOnVinylFinishedComponent, VinylFinishedEvent>(OnVinylFinished);
    }

    private void OnVinylFinished(Entity<TriggerOnVinylFinishedComponent> ent, ref VinylFinishedEvent ev) => Trigger.Trigger(ent.Owner, ev.Player, ent.Comp.KeyOut);
}

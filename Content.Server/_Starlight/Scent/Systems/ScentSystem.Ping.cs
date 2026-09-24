using Content.Shared._Starlight.Scent.Components;
using Content.Shared._Starlight.Scent.Events;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Scent.Systems;

public sealed partial class ScentSystem
{
    private void SendScentSourcePing(Entity<SmellerComponent> smeller, string scentId)
    {
        if (smeller.Comp.Perception != ScentPerception.Full || !TryResolveScentOwner(scentId, out var owner))
            return;

        if (!TryComp(smeller.Owner, out ActorComponent? actor))
            return;

        RaiseNetworkEvent(new ScentSourcePingEvent(scentId, GetNetCoordinates(Transform(owner).Coordinates)), actor.PlayerSession);
    }
}

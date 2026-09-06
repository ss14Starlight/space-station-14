using Content.Shared._Starlight.RedundantMovement;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.RedundantMovement;

public interface IClientRedundantMovementManager
{
    GameTick ServerAckTick { get; set; }

    void Initialize();

    void SendTickData(GameTick tick, IEnumerable<TickInputData> data);
}

using Content.Shared._Starlight.RedundantMovement;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.RedundantMovement;

public interface IClientRedundantMovementManager
{
    uint ServerAckSequence { get; set; }

    void Initialize();

    void SendTickData(GameTick tick, uint sequence, IEnumerable<TickInputData> data);
}

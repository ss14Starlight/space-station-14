using Content.Server._Starlight.Physics;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.RedundantMovement;

public interface IServerRedundantMovementManager
{
    void Initialize();

    void ApplyInput(GameTick tick, SLMoverController mover);
}

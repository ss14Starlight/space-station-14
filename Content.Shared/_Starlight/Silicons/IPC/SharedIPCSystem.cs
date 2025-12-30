// IPC System - Main (Shared)
// SOURCE: Far-Horizons-SS14
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135
// _STARLIGHT: Namespace changes, thermal system will be added later

using Content.Shared._Starlight.Silicons.IPC.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Silicons.IPC;

public abstract partial class SharedIPCSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SetupBattery();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateBattery(frameTime);
    }
}

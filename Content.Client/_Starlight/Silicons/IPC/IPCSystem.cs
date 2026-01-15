// IPC System (Client)
// SOURCE: Far-Horizons-SS14
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135
// _STARLIGHT: Namespace changes for compatibility

using Content.Shared._Starlight.Silicons.IPC;

namespace Content.Client._Starlight.Silicons.IPC;

public sealed partial class IPCSystem : SharedIPCSystem
{
    protected override void UpdateBattery(float frameTime) { }
}


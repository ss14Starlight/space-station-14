// IPC System - Main (Server)
// SOURCE: Far-Horizons-SS14
// https://github.com/Far-Horizons-SS14/Far-Horizons-SS14/pull/135
// _STARLIGHT: Namespace changes for compatibility

using Content.Server.DoAfter;
using Content.Shared._Starlight.Silicons.IPC;

namespace Content.Server._Starlight.Silicons.IPC;

/// <inheritdoc/>
public sealed partial class IPCSystem : SharedIPCSystem 
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
}

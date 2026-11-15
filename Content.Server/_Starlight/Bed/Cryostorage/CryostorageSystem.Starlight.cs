using Content.Server._Starlight.Station.Systems;
using Robust.Shared.IoC;

// ReSharper disable CheckNamespace
namespace Content.Server.Bed.Cryostorage;

public sealed partial class CryostorageSystem
{
    [Dependency] private ContainerSpawnJobSlotSystem _containerSpawnJobSlots = default!;
}

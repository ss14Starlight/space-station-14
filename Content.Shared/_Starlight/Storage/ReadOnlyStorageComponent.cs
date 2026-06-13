using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Storage;

// storage players can take from but not put into. forced inserts still go through, so code can fill it
[RegisterComponent, NetworkedComponent, Access(typeof(ReadOnlyStorageSystem))]
public sealed partial class ReadOnlyStorageComponent : Component
{
}

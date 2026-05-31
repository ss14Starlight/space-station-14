using JetBrains.Annotations;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Shipyard;

[NetSerializable, Serializable]
public enum ShipyardConsoleUiKey : byte
{
    Shipyard,
    //Not currently implemented. Could be used in the future to give other factions a variety of shuttle options.
    Syndicate
}

[UsedImplicitly]
public abstract class SharedShipyardSystem : EntitySystem
{
}

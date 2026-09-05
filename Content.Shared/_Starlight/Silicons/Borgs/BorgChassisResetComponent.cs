using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Present on a borg that has reset its chassis at least once. Modules on a re-picked chassis still work,
/// but they no longer hand out the items a player could take out of them, so a chassis cannot be reset
/// over and over to farm items.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BorgChassisResetComponent : Component;

using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Client -> server: the player opened the construction menu.
/// </summary>
/// <remarks>
/// The menu is entirely client-side, so nothing on the server can see it being opened. Rather than
/// plumb the whole menu through the network for one tutorial beat, the client reports the one bit
/// the curriculum needs. Unsolicited and unverified, which is fine: the worst a forged one can do
/// is skip a tutorial step for the player who forged it.
/// </remarks>
[Serializable, NetSerializable]
public sealed class TutorialConstructionMenuOpenedEvent : EntityEventArgs;

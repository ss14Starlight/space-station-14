using Content.Shared._Starlight.Actions.EntitySystems;

namespace Content.Client._Starlight.Actions.EntitySystems;

/// <summary>
/// Empty on purpose - exists so SharedLatchSystem's handlers also run on the client.
/// </summary>
public sealed partial class LatchSystem : SharedLatchSystem;

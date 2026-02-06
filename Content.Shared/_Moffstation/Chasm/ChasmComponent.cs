using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Moffstation.Chasm;

/// <summary>
///     Marks a component that will cause entities to fall into them on a step trigger activation
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class ChasmComponent : Component
{
    /// <summary>
    /// Items containing components or tags on this list will be rejected by the nest.
    /// </summary>
    [DataField]
    public EntityWhitelist Blacklist = new();

    /// <summary>
    /// Whether blacklisted items should be thrown away from the hole upon rejection
    /// </summary>
    [DataField]
    public bool ThrowBlacklisted;

    /// <summary>
    /// Whether living entities can call into the chasm
    /// </summary>
    [DataField]
    public bool AllowLiving = true;

    /// <summary>
    /// Items containing components or tags on this list will NOT be deleted upon entering the nest, instead being stored until it's destroyed.
    /// </summary>
    [DataField]
    public EntityWhitelist PreservationWhitelist = new();

    /// <summary>
    /// Items containing components or tags on this list will be deleted upon entering the nest, regardless of whether or not they pass the whitelist.
    /// </summary>
    [DataField]
    public EntityWhitelist PreservationBlacklist = new();

    /// <summary>
    /// Whether container's contents should be dumped into the hole separately
    /// </summary>
    [DataField]
    public bool DumpContainers = true;

    /// <summary>
    /// List of objects currently falling into the chasm
    /// </summary>
    [DataField]
    public HashSet<EntityUid> FallingObjects = new();

    [DataField]
    public string HoleContainerId = "chasm-hole";

    /// <summary>
    /// The container that whitelisted items get stored in upon falling. If the entity is destroyed everything in this will be dumped out
    /// </summary>
    public Container Hole = new();

    /// <summary>
    ///     Sound that should be played when an entity falls into the chasm
    /// </summary>
    [DataField("fallingSound")]
    public SoundSpecifier FallingSound = new SoundPathSpecifier("/Audio/Effects/falling.ogg");
}

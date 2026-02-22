using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Equipment.Components;

/// <summary>
/// A piece of equipment that grabs entities and stores them
/// inside of a container so large objects can be moved.
/// Functions analogous to MechGrabber, without a UI element.
/// </summary>
[RegisterComponent]
public sealed partial class LargeGrabberComponent : Component
{
    /// <summary>
    /// The change in energy after each grab.
    /// </summary>
    [DataField]
    public float GrabEnergyCost = 30;

    /// <summary>
    /// How long does it take to grab something?
    /// </summary>
    [DataField]
    public float GrabDelay = 2.5f;

    /// <summary>
    /// The offset from the mech when an item is dropped.
    /// This is here for things like lockers and vendors
    /// </summary>
    [DataField]
    public Vector2 DepositOffset = new(0, -1);

    /// <summary>
    /// The maximum amount of items that can be fit in this grabber
    /// </summary>
    [DataField]
    public int MaxContents = 10;

    /// <summary>
    /// The sound played when a mech is grabbing something
    /// </summary>
    [DataField]
    public SoundSpecifier GrabSound = new SoundPathSpecifier("/Audio/Mecha/sound_mecha_hydraulic.ogg");

    /// <summary>
    /// Blacklists (prevents) entities listed from being grabbed.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist = new()
    {
        Components =
        [
            "WallMount",
            "Anomaly",
            "Mech",
            "MobState",
        ],
    };
    
    /// <summary>
    /// Does the grabber drop everything when put away?
    /// </summary>
    [DataField]
    public bool DropOnContainerChange = false;

    public EntityUid? AudioStream;

    [ViewVariables(VVAccess.ReadWrite)]
    public Container ItemContainer = default!;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public DoAfterId? DoAfter;
}

using Content.Server.StationEvents.Events;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(WreckSwarmSystem)), AutoGenerateComponentPause]
public sealed partial class WreckSwarmComponent : Component
{
    [DataField]
    public float Velocity = 20f;

    /// <summary>
    /// The announcement played when a meteor swarm begins.
    /// </summary>
    [DataField]
    public LocId? Announcement = "station-event-incoming-wreck-announcement";

    [DataField]
    public SoundSpecifier? AnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/meteors.ogg")
    {
        Params = new()
        {
            Volume = -4
        }
    };

    /// <summary>
    /// The size of wreck this should select from, mapping to <see cref="SalvageMapPrototype.SizeString"/>.
    /// </summary>
    [DataField]
    public LocId? SizeFilter;

    /// <summary>
    /// The fixed grid that should be spawned in this case; overrides SizeFilter-based selection.
    /// </summary>
    [DataField]
    public ResPath? FixedGrid;
}

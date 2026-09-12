using Content.Server._Starlight.StationEvents.Events;
using Content.Shared._Starlight.Salvage.Ruins;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.StationEvents.Components;

[RegisterComponent, Access(typeof(WreckSwarmSystem))]
public sealed partial class WreckSwarmComponent : Component
{
    /// <summary>
    /// World-space speed applied toward the station, matching the original wreck swarm.
    /// </summary>
    [DataField]
    public float Velocity = 50f;

    /// <summary>
    /// The announcement played when a wreck swarm begins.
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
    /// Ruin chunk size config (<c>Small</c> / <c>Medium</c> / <c>Large</c>).
    /// </summary>
    [DataField]
    public ProtoId<RuinChunkConfigPrototype>? ChunkConfig;

    /// <summary>
    /// The fixed grid that should be spawned in this case; overrides ruin generation.
    /// </summary>
    [DataField]
    public ResPath? FixedGrid;
}

using Robust.Shared.Prototypes;
using Content.Shared.Radio;

namespace Content.Server.StationEvents.Components;

/// <summary>
/// Spawns a single entity at a random tile on a station using TryGetRandomTile.
/// </summary>
public sealed partial class RandomSpawnRuleComponent : Component
{
    /// <summary>
    /// Retry tiles blocked by hard fixtures that collide with ordinary mobs.
    /// If no clear tile is found, skip the spawn.
    /// </summary>
    [DataField]
    public bool RequireUnobstructedTile;

    // Moffstation - Syndicate dead drop
    /// <summary>
    /// The radio message to send when spawning the entity. The entity is used as the sender of the radio message.
    /// </summary>
    [DataField]
    public RandomSpawnRuleRadioMessage? RadioMessage;
}

// Moffstation - Syndicate dead drop
/// <param name="Channel">The channel to send the message over</param>
/// <param name="Message">The message to send. Is localized with a <c>location</c> argument.</param>
[DataRecord]
public sealed partial record RandomSpawnRuleRadioMessage(
    [field: DataField(required: true)]
    ProtoId<RadioChannelPrototype> Channel,
    [field: DataField(required: true)]
    LocId Message
);

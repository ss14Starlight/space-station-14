//Copyright © 2025 .cerol (Discord), Licensed under MIT License.
//Changes after https://github.com/ss14Starlight/space-station-14/pull/2054/commits/e18dafedad110b20cdc17d054fe35413a1831f59 licensed under Starlight License.

using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.PinataSystem;

/// <summary>
/// Makes the entity throw items when someone hits it.
/// </summary>
[RegisterComponent]
public sealed partial class PinataComponent : Component
{
    /// <summary>
    /// The entity table to select loot from when entity hitten by someone.
    /// </summary>
    [DataField]
    public EntityTableSelector? HitTable;
    
    /// <summary>
    /// The entity table to select loot from when entity gibbed.
    /// </summary>
    [DataField]
    public EntityTableSelector? GibTable;
}

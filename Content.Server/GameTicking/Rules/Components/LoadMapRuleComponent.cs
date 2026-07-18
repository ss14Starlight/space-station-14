using Content.Shared.Maps;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

#region Starlight
using Content.Shared.Tag;
#endregion Starlight

namespace Content.Server.GameTicking.Rules.Components;

/// <summary>
/// This is used for a game rule that loads a map when activated.
/// Works with <see cref="RuleGridsComponent"/>.
/// Exactly one of <see cref="GameMap"/>, <see cref="MapPath"/>, or <see cref="GridPath"/> should be set.
/// </summary>
[RegisterComponent, Access(typeof(LoadMapRuleSystem))]
public sealed partial class LoadMapRuleComponent : Component
{
    /// <summary>
    /// A <see cref="GameMapPrototype"/> to load on a new map.
    /// </summary>
    [DataField]
    public ProtoId<GameMapPrototype>? GameMap;

    /// <summary>
    /// A map to load.
    /// </summary>
    [DataField]
    public ResPath? MapPath;

    /// <summary>
    /// A grid to load on a new map.
    /// </summary>
    [DataField]
    public ResPath? GridPath;

    /// <summary>
    /// Starlight - If a map with the tag below already exist, we do not load a new one and give info on the current one.
    /// THIS ONLY SUPPORT "MapPath"
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype>? MapTag;
}

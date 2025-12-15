using Content.Server.GameTicking.Rules;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Maths;
using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Magic.Components;

/// <summary>
/// Component for the Wizard Battle game rule that loads two wizard shuttles.
/// </summary>
[RegisterComponent]
public sealed partial class WizardBattleRuleComponent : Component
{
    /// <summary>
    /// Path to the wizards shuttle grids.
    /// </summary>
    [DataField]
    public ResPath ShuttlePathBlue = new("/Maps/_Starlight/ArchMageShuttleBlue.yml");

    [DataField]
    public ResPath ShuttlePathRed = new("/Maps/_Starlight/ArchMageShuttleRed.yml");

    /// <summary>
    /// Offset for the red faction shuttle.
    /// </summary>
    [DataField]
    public Vector2 RedOffset = new(-1000, 0);

    /// <summary>
    /// Offset for the blue faction shuttle.
    /// </summary>
    [DataField]
    public Vector2 BlueOffset = new(1000, 0);

    /// <summary>
    /// Temporary storage for loaded shuttle entities.
    /// </summary>
    [DataField]
    public EntityUid? RedShuttle;

    [DataField]
    public EntityUid? BlueShuttle;

    /// <summary>
    /// Temporary map ID for loading shuttles.
    /// </summary>
    [DataField]
    public MapId? TempMapId;
}
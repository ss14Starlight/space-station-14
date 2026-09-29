using Content.Server.GameTicking.Presets;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.GameTicking.Rules.Components;

/// <summary>
/// Defines a shared round-based cooldown for a Dynamic rule, related rules, and game presets.
/// </summary>
[RegisterComponent]
public sealed partial class DynamicRuleCooldownComponent : Component
{
    /// <summary>
    /// The number of subsequent eligible rounds for which this cooldown applies.
    /// </summary>
    [DataField(required: true)]
    public int Cooldown;

    /// <summary>
    /// Other game rules which share this cooldown with the owning rule.
    /// Selecting any rule in the group activates the cooldown for the whole group.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> Rules = [];

    /// <summary>
    /// Game presets which share this cooldown with the owning rule.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<GamePresetPrototype>> Presets = [];

    /// <summary>
    /// Whether non-Dynamic rounds count down this cooldown.
    /// </summary>
    [DataField]
    public bool DecrementOnNonDynamicRounds = true;
}

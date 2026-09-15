using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;

namespace Content.Server._Starlight.GameTicking;

/// <summary>
/// Raised after <see cref="GameTicker"/> locks in a preset, before its rules are started and validated.
/// </summary>
public readonly record struct DynamicRuleCooldownRoundInitializingEvent(GamePresetPrototype Preset);

/// <summary>
/// Raised after the locked-in preset successfully passes round-start validation.
/// </summary>
public readonly record struct DynamicRuleCooldownRoundStartedEvent(GamePresetPrototype Preset);

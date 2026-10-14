using Content.Shared.Antag;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.GameTicking.Events;

/// <summary>
/// Raised when a player has a valid job character, but that character cannot satisfy one or more
/// preselected antagonist roles. The listed antagonist reservations should be released while the
/// player continues spawning in their assigned job.
/// </summary>
public readonly record struct InvalidAntagProfileSpawningEvent(ICommonSession Player, IReadOnlySet<ProtoId<AntagSpecifierPrototype>> InvalidAntags);

using Robust.Shared.Player;

namespace Content.Server._Starlight.CosmicCult;

[ByRefEvent]
public record struct CosmicCultVoterEligibilityEvent(ICommonSession Player, bool Eligible);

using Robust.Shared.Player;

namespace Content.Server._ST.CosmicCult;

[ByRefEvent]
public record struct CosmicCultVoterEligibilityEvent(ICommonSession Player, bool Eligible);

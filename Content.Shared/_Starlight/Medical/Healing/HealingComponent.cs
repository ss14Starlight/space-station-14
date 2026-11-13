using Content.Shared.Chemistry.Reagent;
using Content.Shared.Whitelist;

namespace Content.Shared.Medical.Healing;
public sealed partial class HealingComponent : Component
{
    [DataField]
    public bool SolutionDrain = false;

    [DataField]
    public List<ReagentQuantity> ReagentsToDrain = new();

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}

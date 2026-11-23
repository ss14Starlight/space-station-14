using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Cargo;

public abstract partial class SharedCargoSystem
{
    /// <summary>
    /// Lets an order console show products of a market. The station still has to unlock that market before it can be ordered.
    /// </summary>
    public void AllowMarket(Entity<CargoOrderConsoleComponent?> console, ProtoId<CargoMarketPrototype> market)
    {
        if (!Resolve(console, ref console.Comp, false) || console.Comp.AllowedGroups.Contains(market))
            return;

        console.Comp.AllowedGroups.Add(market);
        Dirty(console);
    }
}

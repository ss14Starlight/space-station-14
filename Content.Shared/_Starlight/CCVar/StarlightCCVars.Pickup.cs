using Robust.Shared.Configuration;

// ReSharper disable CheckNamespace 
namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    public static readonly CVarDef<float> MaxPickupDifference =
        CVarDef.Create("game.max_pickup_difference", 0.995f, CVar.REPLICATED | CVar.SERVER);
    
}
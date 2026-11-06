using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Station;

public static class CorgiCollarHelper
{
    /// <summary>
    /// Collar equivalent of a PDA id (ChemistryPDA -> ChemistryCollarPDA), or null if none exists.
    /// </summary>
    public static string? GetCollarEquivalent(string pdaId, IPrototypeManager protoManager)
    {
        var collarId = pdaId.EndsWith("PDA") ? pdaId[..^3] + "CollarPDA" : pdaId + "CollarPDA";
        return protoManager.HasIndex<EntityPrototype>(collarId) ? collarId : null;
    }
}

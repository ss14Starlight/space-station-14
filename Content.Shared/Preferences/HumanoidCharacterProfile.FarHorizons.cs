using System.IO;
using System.Linq;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    public const string SpeciesLoadoutDatabaseKey = "__species_loadout"; // Database will store species loadout as this "job"

    [DataField]
    public RoleLoadout? SpeciesLoadout = null;

    public HumanoidCharacterProfile WithSpeciesLoadout(RoleLoadout? speciesLoadout) => 
    new(this) { SpeciesLoadout = speciesLoadout, };

    public RoleLoadout? GetSpeciesLoadoutOrDefault(ICommonSession? session, IPrototypeManager protoManager)
    {
        var speciesProto = protoManager.Index(Species);
        if (speciesProto.Loadout == null)
        {
            SpeciesLoadout = null;
            return SpeciesLoadout;
        }

        if (SpeciesLoadout == null)
        {
            SpeciesLoadout = new RoleLoadout(speciesProto.Loadout.Value);
            SpeciesLoadout.SetDefault(this, session, protoManager, force: true);
        }

        SpeciesLoadout.SetDefault(this, session, protoManager);
        return SpeciesLoadout;
    }

    private static bool SpeciesLoadoutEquals(RoleLoadout? A, RoleLoadout? B)
    {
        if (A == null != (B == null))
            return false;

        if (A != null && B != null)
        {   
            if (A.SelectedLoadouts.Count != B.SelectedLoadouts.Count)
                return false;
            
            foreach (var (k, v) in A.SelectedLoadouts)
                if (!B.SelectedLoadouts.TryGetValue(k, out var bValue) || !bValue.SequenceEqual(v))
                    return false;
        }

        return true;
    }
}
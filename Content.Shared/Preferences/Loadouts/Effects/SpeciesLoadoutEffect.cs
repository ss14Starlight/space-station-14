using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

public sealed partial class SpeciesLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public List<ProtoId<SpeciesPrototype>> Species = new();

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session, IDependencyCollection collection,
        out FormattedMessage reason) // Starlight: Always return reason
    {
        // Starlight BEGIN
        var protoManager = collection.Resolve<IPrototypeManager>();
        var sb = new StringBuilder();
        foreach (var s in Species)
            sb.Append(Loc.GetString(protoManager.Index(s).Name) + " ");

        var success = Species.Contains(profile.Species);
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            success ? "loadout-species-restriction-pass" : "loadout-species-restriction-fail",
            ("species", sb)));
        return success;
        // Starlight END
    }
}

using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

public sealed partial class PointsCostLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public int Cost = 1;

    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        out FormattedMessage reason) // Starlight: Always return reason
    {
        reason = FormattedMessage.Empty; // Starlight. Keeping changes in this file minimal as its entirely unused.
        var protoManager = collection.Resolve<IPrototypeManager>();

        if (!protoManager.TryIndex(loadout.Role, out var roleProto) || roleProto.Points == null)
        {
            return true;
        }

        if (loadout.Points <= Cost)
        {
            reason = FormattedMessage.FromUnformatted("loadout-group-points-insufficient");
            return false;
        }

        return true;
    }

    public override void Apply(RoleLoadout loadout)
    {
        loadout.Points -= Cost;
    }
}

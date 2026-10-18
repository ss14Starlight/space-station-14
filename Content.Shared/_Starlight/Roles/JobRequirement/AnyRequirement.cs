using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Roles;

/// <summary>
/// Requires at least one nested requirement to pass, or none when inverted.
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class AnyRequirement : JobRequirement
{
    [DataField(required: true)]
    public List<JobRequirement> Requirements = new();

    public override bool Check(
        IEntityManager entManager,
        ICommonSession? player,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan>? playTimes,
        out FormattedMessage reason)
    {
        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            Inverted ? "role-requirement-none" : "role-requirement-any"));

        var anyMet = false;
        foreach (var requirement in Requirements)
        {
            anyMet |= requirement.Check(entManager, player, protoManager, profile, playTimes, out var childReason);
            reason.PushNewline();
            reason.AddMessage(childReason);
        }

        return anyMet != Inverted;
    }
}

using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

/// <summary>
/// Uses a <see cref="LoadoutEffectGroupPrototype"/> prototype as a singular effect that can be re-used.
/// </summary>
public sealed partial class GroupLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public ProtoId<LoadoutEffectGroupPrototype> Proto;

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session, IDependencyCollection collection, out FormattedMessage reason) // Starlight: Always return reason
    {
        var effectsProto = collection.Resolve<IPrototypeManager>().Index(Proto);

        reason = new FormattedMessage(); // Starlight
        var success = true; // Starlight
        foreach (var effect in effectsProto.Effects)
        {
            // Starlight BEGIN
            if (!effect.Validate(profile, loadout, session, collection, out var effectReason))
                success = false;
            
            if (!reason.IsEmpty)
                reason.PushNewline();
            reason.AddMessage(effectReason);
            // Starlight END
        }

        return success; // Starlight
    }
}

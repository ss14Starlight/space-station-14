using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Traits.Effects;

/// <summary>
/// Adds permanent actions to the entity.
/// </summary>
public sealed partial class AddActionsEffect : BaseTraitEffect
{
    [DataField(required: true)]
    public HashSet<EntProtoId> Actions { get; private set; } = new();

    public override void Apply(TraitEffectContext ctx)
    {
        if (!ctx.EntMan.EntitySysManager.TryGetEntitySystem(out SharedActionsSystem? actionsSystem))
            return;

        foreach (var action in Actions)
            actionsSystem.AddAction(ctx.Player, action.Id);
    }
}
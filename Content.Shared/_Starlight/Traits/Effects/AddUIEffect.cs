namespace Content.Shared._Starlight.Traits.Effects;

/// <summary>
/// Adds permanent user interfaces to the entity.
/// </summary>
public sealed partial class AddUIEffect : BaseTraitEffect
{
    /// <summary>
    /// The set of UIs to add to the entity.
    /// </summary>
    [DataField(required: true), AlwaysPushInheritance]
    internal Dictionary<Enum, InterfaceData> Interfaces = new();

    /// <inheritdoc/>
    public override void Apply(TraitEffectContext ctx)
    {
        if (!ctx.EntMan.EntitySysManager.TryGetEntitySystem(out SharedUserInterfaceSystem? uiSystem))
            return;

        foreach (var kv in Interfaces)
            uiSystem.SetUi(ctx.Player, kv.Key, kv.Value);
    }
}

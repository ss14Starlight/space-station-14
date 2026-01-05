namespace Content.Server._Starlight.Paper;

[ImplicitDataDefinitionForInheritors]
public abstract partial class OnSignAction
{
    /// <summary>
    /// Whether this action targets exclusively the paper and will be ran only once instead of on each entity that signed it
    /// </summary>
    [DataField("targetPaper")]
    public bool TargetsPaper = false;

    /// <summary>
    /// whether this Action has allready been IoC injected. this should only be touched by the system and not by the Action it'self
    /// </summary>
    public bool IoCInjected = false;

    /// <summary>
    /// what should this action do to each entity. if TargetsPaper is true. paper will be the same as target.
    /// </summary>
    /// <param name="paper">the paper with the component</param>
    /// <param name="component">the actions on sign component</param>
    /// <param name="target">the person who signed it</param>
    /// <returns>wheter or not to "break" and skip running other actions</returns>
    public abstract bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target);

    /// <summary>
    /// has the Action self-IoC resolve and fill out fields. this should only be called once.
    /// </summary>
    public abstract void ResolveIoC();
}

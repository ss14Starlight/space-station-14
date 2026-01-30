using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionAddComponent : OnSignAction
{
    /// <summary>
    /// list of component names to be added.
    /// </summary>
    [DataField]
    public ComponentRegistry Components = [];
    
    private IEntityManager _entityManager = default!;
    
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        _entityManager.AddComponents(target, Components);
        return false;
    }

    public override void ResolveIoC()
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
    }
}
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionSpawnEntity : OnSignAction
{
    /// <summary>
    /// what entities should be summoned on the target.
    /// </summary>
    [DataField]
    public List<EntProtoId> Entities = [];
    
    private IEntityManager _entityManager = default!;
    
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        if (!_entityManager.TryGetComponent<TransformComponent>(target, out var xform))
            return false; // The signer somehow does not have a position? so we cant spawn stuff on em.
        foreach (var entity in Entities)
        {
            _entityManager.SpawnAtPosition(entity, xform.Coordinates);
        }
        return false;
    }

    public override void ResolveIoC()
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
    }
}
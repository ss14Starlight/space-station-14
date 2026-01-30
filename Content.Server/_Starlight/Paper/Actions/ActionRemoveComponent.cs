using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionRemoveComponent : OnSignAction
{
    /// <summary>
    /// list of component names to be deleted.
    /// </summary>
    [DataField(customTypeSerializer: typeof(CustomHashSetSerializer<string, ComponentNameSerializer>))]
    public HashSet<string> Components = [];

    private IComponentFactory _componentFactory = default!;
    private IEntityManager _entityManager = default!;

    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        foreach (var rmComp in Components)
        {
            if (_componentFactory.TryGetRegistration(rmComp, out var registration))
                _entityManager.RemoveComponent(target, registration.Type);
        }

        return false;
    }

    public override void ResolveIoC()
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _componentFactory = IoCManager.Resolve<IComponentFactory>();
    }
}

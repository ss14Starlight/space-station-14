using Content.Shared._Starlight.EntityConditions.Conditions.Body;
using Content.Shared.EntityConditions;

namespace Content.Server._Starlight.EntityConditions.Conditions;

/// <summary>
/// Returns true if this entity has specified components.
/// </summary>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class HasComponentsEntityConditionSystem : EntityConditionSystem<MetaDataComponent, HasComponentsCondition>
{
    [Dependency] private EntityManager _entity = default!;

    protected override void Condition(Entity<MetaDataComponent> entity, ref EntityConditionEvent<HasComponentsCondition> args)
    {
        foreach (var (_, registration) in args.Condition.Components)
        {
            if (_entity.HasComponent(entity.Owner, registration.Component.GetType()))
            {
                if (!args.Condition.All)
                {
                    args.Result = true;
                    return;
                }
            }
            else if (args.Condition.All)
            {
                args.Result = false;
                return;
            }
        }

        args.Result = args.Condition.All;
    }
}

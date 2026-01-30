using Content.Shared._Starlight.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.EntityTable.Conditions;

/// <summary>
/// EntityTable condition: spawns if the specified holiday is currently active.
/// </summary>
[DataDefinition]
public sealed partial class HolidayActiveCondition : EntityTableCondition
{
    [DataField("holiday", required: true)]
    public string Holiday = string.Empty;

    protected override bool EvaluateImplementation(EntityTableSelector root, IEntityManager entMan, IPrototypeManager proto, EntityTableContext ctx)
    {
        var sys = entMan.System<HolidayConditionSystem>();
        return sys.CheckHoliday(Holiday);
    }
}

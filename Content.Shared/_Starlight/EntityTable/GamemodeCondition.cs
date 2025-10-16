using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.EntityTable;

/// <summary>
/// Condition that passes only if the server player count is within a certain range.
/// </summary>
public sealed partial class GamemodeCondition : EntityTableCondition
{
    /// <summary>
    /// Minimum players of needed for this condition to succeed. Inclusive.
    /// </summary>
    [DataField(required: true)]
    public HashSet<string> Presets = [];
    
    private static GamemodeConditionSystem? _conditionSystem;

    protected override bool EvaluateImplementation(EntityTableSelector root, IEntityManager entMan, IPrototypeManager proto, EntityTableContext ctx)
    {
        // Don't resolve this repeatedly
        _conditionSystem ??= entMan.System<GamemodeConditionSystem>();

        return _conditionSystem.CheckGamemode(Presets);
    }
}



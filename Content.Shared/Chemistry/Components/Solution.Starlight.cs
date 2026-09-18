using System.Collections;
using Content.Shared._Blimpuf.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Components
{
    /// <summary>
    ///     A solution of reagents.
    /// </summary>
    public sealed partial class Solution : IEnumerable<ReagentQuantity>, ISerializationHooks, IRobustCloneable<Solution>
    {
        // Blimpuf start
        private static Color GetReagentColor(ReagentPrototype proto, ReagentId reagent)
        {
            if (reagent.Data == null)
                return proto.SubstanceColor;

            foreach (var data in reagent.Data)
            {
                if (data is ReagentColorData colorData)
                    return colorData.Color;
            }

            return proto.SubstanceColor;
        }
        // Blimpuf end
    }
}

using System.Collections;
using System.Linq;
using Content.Shared._Blimpuf.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Components
{
    /// <summary>
    ///     A solution of reagents.
    /// </summary>
    public sealed partial class Solution : IEnumerable<ReagentQuantity>, ISerializationHooks, IRobustCloneable<Solution>
    {
        // Funky start
        public int GetSolutionFlammability(IPrototypeManager? protoMan)
        {
            if (Volume <= 0)
                return 0;

            IoCManager.Resolve(ref protoMan);
            var totalFlammability = 0f;
            foreach (var (reagent, quantity) in Contents)
            {
                if (protoMan.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto))
                {
                    totalFlammability += proto.Flammability * (quantity.Float() / Volume.Float());
                }
            }
            return (int) MathF.Round(totalFlammability);
        }

        public bool IsSolutionSelfOxidizing(IPrototypeManager? protoMan)
        {
            if (Volume <= 0)
                return false;

            IoCManager.Resolve(ref protoMan);
            foreach (var (reagent, _) in Contents)
            {
                if (protoMan.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto) && proto.SelfOxidizing)
                {
                    return true;
                }
            }
            return false;
        }

        public void BurnFlammableReagents(float fraction, IPrototypeManager? protoMan)
        {
            IoCManager.Resolve(ref protoMan);
            var clone = Clone();
            foreach (var (reagent, quantity) in Contents)
            {
                if (!protoMan.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto) || proto.Flammability <= 0)
                    continue;

                var rawBurn = quantity.Float() * fraction * proto.Flammability;
                var roundedBurn = MathF.Ceiling(rawBurn * 100f) / 100f;
                if (roundedBurn <= 0f)
                    continue;

                clone.RemoveReagent(reagent, FixedPoint2.New(roundedBurn));
            }
            Contents = clone.Contents;
            Volume = clone.Volume;
            _heatCapacityDirty = true;
            ValidateSolution();
        }
        // Funky end

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

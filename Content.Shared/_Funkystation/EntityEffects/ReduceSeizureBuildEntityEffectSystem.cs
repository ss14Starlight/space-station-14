// SPDX-FileCopyrightText: 2026 Mora <46364955+TrixxedHeart@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared._Funkystation.EntityEffects.Effects;
using Content.Shared._Funkystation.Traits.Assorted;
using Content.Shared.EntityEffects;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared._Funkystation.EntityEffects;

public sealed partial class ReduceSeizureBuildEntityEffectSystem : EntityEffectSystem<NeuroAversionComponent, ReduceSeizureBuild>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    protected override void Effect(Entity<NeuroAversionComponent> entity, ref EntityEffectEvent<ReduceSeizureBuild> args)
    {
        var actualReduction = args.Effect.ReductionAmount * args.Scale;
        actualReduction = MathF.Max(0.001f, MathF.Min(1.0f, actualReduction));

        // Psicodine is the reagent wired to this effect. The current effect event has no reagent context.
        entity.Comp.SeizurePaused = _solutions.TryGetSolution(entity.Owner, "bloodstream", out _, out var bloodstream)
                                    && bloodstream.ContainsReagent(new ReagentId("Psicodine", null));

        var amountToSubtract = MathF.Min(actualReduction, entity.Comp.SeizureBuild);
        entity.Comp.SeizureBuild -= amountToSubtract;
        Dirty(entity);
    }
}

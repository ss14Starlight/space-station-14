// SPDX-FileCopyrightText: 2025 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared._Funkystation.EntityEffects.Effects;
using Content.Shared._Funkystation.Traits.Assorted;
using Content.Shared.EntityEffects;

namespace Content.Shared._Funkystation.EntityEffects;

public sealed partial class ReduceSeizureBuildEntityEffectSystem : EntityEffectSystem<NeuroAversionComponent, ReduceSeizureBuild>
{
    protected override void Effect(Entity<NeuroAversionComponent> entity, ref EntityEffectEvent<ReduceSeizureBuild> args)
    {
        var actualReduction = args.Effect.ReductionAmount * args.Scale;
        actualReduction = MathF.Max(0.001f, MathF.Min(1.0f, actualReduction));
        var amountToSubtract = MathF.Min(actualReduction, entity.Comp.SeizureBuild);
        entity.Comp.ModifySeizureBuild(-amountToSubtract);
    }
}

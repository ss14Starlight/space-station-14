// SPDX-FileCopyrightText: 2026 Mora <46364955+TrixxedHeart@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.EntityEffects.Effects;

public sealed partial class ReduceSeizureBuild : EntityEffectBase<ReduceSeizureBuild>
{
    [DataField("reductionAmount")]
    public float ReductionAmount = 0.1f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc)
        => loc.GetString("blimpuf-reagent-effect-reduce-seizure-build", ("chance", Probability));
}

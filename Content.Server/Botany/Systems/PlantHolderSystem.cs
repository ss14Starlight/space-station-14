using JetBrains.Annotations;
using Content.Server.Botany.Components;
using Content.Server.Popups;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Botany;
using Content.Shared.Burial.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Content.Shared.Popups;
using Content.Shared.Random;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Server.Botany.Systems;

/// <summary>
/// API for runtime plant lifecycle state.
/// </summary>
public sealed class PlantHolderSystem : EntitySystem
{
    [Dependency] private readonly PlantSystem _plant = default!;

    /// <summary>
    /// Adjusts the health of the plant.
    /// </summary>
    [PublicAPI]
    public void AdjustsHealth(Entity<PlantHolderComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (!TryComp<PlantComponent>(ent.Owner, out var plant))
            return;

        ent.Comp.Health += MathHelper.Clamp(amount, 0, plant.Endurance);
        CheckHealth(ent);
        _plant.UpdateSprite(ent.Owner);
    }

    /// <summary>
    /// Adjusts the mutation level of the plant.
    /// </summary>
    [PublicAPI]
    public void AdjustsMutationLevel(Entity<PlantHolderComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.MutationLevel += amount * ent.Comp.MutationMod;
        CheckHealth(ent);
    }

    /// <summary>
    /// Adjusts the mutation mod of the plant.
    /// </summary>
    [PublicAPI]
    public void AdjustsMutationMod(Entity<PlantHolderComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.MutationMod += amount;
    }

    /// <summary>
    /// Adjusts the pests of the plant.
    /// </summary>
    [PublicAPI]
    public void AdjustsPests(Entity<PlantHolderComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.PestLevel += amount;
    }

    /// <summary>
    /// Adjusts the age of the plant.
    /// </summary>
    [PublicAPI]
    public void AdjustsAge(Entity<PlantHolderComponent?> ent, int amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.Age += amount;
        _plant.UpdateSprite(ent.Owner);
    }

    /// <summary>
    /// Adjusts the toxins of the plant.
    /// </summary>
    [PublicAPI]
    public void AdjustsToxins(Entity<PlantHolderComponent?> ent, float amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.Toxins += amount;
    }

    /// <summary>
    /// Checks if the plant is dead.
    /// </summary>
    [PublicAPI]
    public bool IsDead(Entity<PlantHolderComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return false;

        return ent.Comp.Dead;
    }

    /// <summary>
    /// Checks if the plant is dead.
    /// </summary>
    [PublicAPI]
    public void CheckHealth(Entity<PlantHolderComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        if (ent.Comp.Health <= 0)
            Die(ent);
    }

    /// <summary>
    /// Kills the plant.
    /// </summary>
    [PublicAPI]
    public void Die(Entity<PlantHolderComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.Dead = true;
        ent.Comp.Health = Math.Max(0, ent.Comp.Health);

        if (TryComp<PlantHarvestComponent>(ent.Owner, out var harvest))
            harvest.ReadyForHarvest = false;

        ent.Comp.MutationLevel = 0;
        ent.Comp.YieldMod = 1;
        ent.Comp.MutationMod = 1;
    }
}

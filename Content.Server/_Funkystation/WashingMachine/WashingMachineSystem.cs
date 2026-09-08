using Content.Shared._Funkystation.WashingMachine;
using Content.Shared._Funkystation.Stains.Components;
using Content.Shared._Funkystation.Stains.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.Storage.Components;
using Content.Server.Forensics;
using Content.Shared.Clothing.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Chemistry;
using Content.Shared.Damage.Systems;

namespace Content.Server._Funkystation.WashingMachine;

public sealed partial class WashingMachineSystem : SharedWashingMachineSystem
{
    [Dependency] private SharedSolutionContainerSystem _solution = null!;
    [Dependency] private ForensicsSystem _forensics = null!;

    protected override void UpdateForensics(Entity<WashingMachineComponent> ent, HashSet<EntityUid> items)
    {
        if (!TryComp<ForensicsComponent>(ent.Owner, out var forensics))
            return;

        foreach (var item in items)
        {
            // Pull DNA out of the item's stain solution before the shared FinishWash washes it out.
            if (TryComp<StainableComponent>(item, out var stain)
                && _solution.TryGetSolution(item, stain.SolutionName, out var sol))
            {
                forensics.DNAs.UnionWith(_forensics.GetSolutionsDNA(sol.Value.Comp.Solution));
            }

            if (!TryComp<FiberComponent>(item, out var fiber))
                continue;

            var fiberText = fiber.FiberColor == null
                ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial))
                : Loc.GetString("forensic-fibers-colored",
                    ("color", fiber.FiberColor),
                    ("material", fiber.FiberMaterial));

            forensics.Fibers.Add(fiberText);
        }
    }
}

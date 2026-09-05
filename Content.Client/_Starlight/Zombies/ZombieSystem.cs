using Content.Shared.StatusIcon.Components;
using Content.Shared.Zombies;

namespace Content.Client.Zombies;

public sealed partial class ZombieSystem
{

    private void OnGetDelayedInitialInfectedIcon(
        Entity<InitialInfectedComponent> ent,
        ref GetStatusIconsEvent args)
    {
        if (!TryComp<BloodStreamInfectionComponent>(ent, out var infection) ||
            !infection.HasBeenBriefed)
        {
            return;
        }

        GetInitialInfectedIcon(ent, ref args);
    }
}

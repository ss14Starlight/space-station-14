using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Humanoid;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Content.Shared._Starlight.Humanoid;

namespace Content.Shared._Starlight.Humanoid;

public sealed partial class PetBedSleepPoseSystem : EntitySystem
{
    [Dependency] private SharedRotationVisualsSystem _rotationVisuals = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PetBedSleepPoseComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<BuckleComponent, SleepStateChangedEvent>(OnSleepStateChanged);
    }

    private void OnStrapped(Entity<PetBedSleepPoseComponent> bed, ref StrappedEvent args)
    {
        var occupant = args.Buckle.Owner;
        if (!HasComp<HumanoidAppearanceComponent>(occupant) || !HasComp<SleepingComponent>(occupant))
            return;

        SetSleepingPose(occupant);
    }

    private void OnSleepStateChanged(Entity<BuckleComponent> ent, ref SleepStateChangedEvent args)
    {
        if (!HasComp<HumanoidAppearanceComponent>(ent)
            || ent.Comp.BuckledTo is not { } bed
            || !HasComp<PetBedSleepPoseComponent>(bed))
        {
            return;
        }

        if (args.FellAsleep)
        {
            SetSleepingPose(ent.Owner);
        }
        else if (TryComp<StrapComponent>(bed, out var strap))
        {
            _rotationVisuals.SetHorizontalAngle(ent.Owner, strap.Rotation);
        }
    }

    private void SetSleepingPose(EntityUid occupant)
    {
        _rotationVisuals.ResetHorizontalAngle(occupant);
        _standing.Down(occupant, playSound: false, dropHeldItems: false, force: true);
    }
}

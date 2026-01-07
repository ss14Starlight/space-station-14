using Content.Shared._FarHorizons.Vehicles.Components;
using Robust.Shared.Audio.Systems;
using Content.Shared.DragDrop;
using Content.Shared.Lock;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Examine;

namespace Content.Shared._FarHorizons.Vehicles.EntitySystems;

public abstract partial class SharedVehicleSystems : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleComponent, TurnKeysEvent>(OnTurnKeysEvent);
        SubscribeLocalEvent<VehicleComponent, HornActionEvent>(OnHornActionEvent);
        SubscribeLocalEvent<VehicleComponent, ToggleTrunkActionEvent>(OnToggleTrunk);
        SubscribeLocalEvent<VehicleComponent, StartCollideEvent>(HandleCollide);
        SubscribeLocalEvent<VehicleComponent, CanDropTargetEvent>(OnCanDragDrop);
        SubscribeLocalEvent<VehicleComponent, ExaminedEvent>(OnExamine);
    }

    protected virtual void OnTurnKeysEvent(Entity<VehicleComponent> ent, ref TurnKeysEvent args)
    {
        if(args.Handled || ent.Comp.StartUp == null) return;
        if(ent.Comp.Rider == null) return;
        if(!ent.Comp.Started)
        {
            _audio.PlayPredicted(ent.Comp.StartUp, ent.Owner, ent.Comp.Rider.Value);
        }
        args.Handled = true;
    }

    private void OnHornActionEvent(Entity<VehicleComponent> ent, ref HornActionEvent args)
    {
        if (args.Handled || ent.Comp.HornSound == null)
            return;
        if(ent.Comp.Rider == null) return;
        _audio.PlayPredicted(ent.Comp.HornSound, ent.Owner, ent.Comp.Rider.Value);
        args.Handled = true;
    }

    public void TryUpdateVisualState(Entity<VehicleComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        var finalState = VehicleVisualState.Normal;
        if (entity.Comp.isMoving)
        {
            finalState = VehicleVisualState.Moving;
        }
        else if (entity.Comp.isBroken)
        {
            finalState = VehicleVisualState.Broken;
        }

        _appearance.SetData(entity.Owner, VehicleVisuals.VisualState, finalState);
    }

    protected virtual void OnToggleTrunk(Entity<VehicleComponent> ent, ref ToggleTrunkActionEvent args)
    {
        if(!TryComp<LockComponent>(ent.Owner, out var lockComp)) return;

        if(!_lock.IsLocked(ent.Owner))
        {
            _audio.PlayPredicted(lockComp.UnlockSound, ent.Owner, ent.Comp.Rider!.Value);
        }
        else
        {
            _audio.PlayPredicted(lockComp.LockSound, ent.Owner, ent.Comp.Rider!.Value);
        }
    }

    protected virtual void HandleCollide(Entity<VehicleComponent> ent, ref StartCollideEvent args)
    {
        if(ent.Comp.Rider == null) return;
        var rider = ent.Comp.Rider.Value;
        
        if(!ent.Comp.AllowCrashing) return;

        var speed = args.OurBody.LinearVelocity.Length();

        if (speed < ent.Comp.CrashingSpeed) return;
        
        if (args.OurFixture.Hard && args.OtherFixture.Hard)
        {
            if (_gameTiming.IsFirstTimePredicted)
                _audio.PlayPvs(ent.Comp.SoundHit, ent.Owner, AudioParams.Default.WithVariation(0.125f).WithVolume(-0.125f));
        }
        else if(args.OurFixture.Hard && !args.OtherFixture.Hard)
        {
            if(HasComp<DamageableComponent>(args.OtherEntity))
                if (_gameTiming.IsFirstTimePredicted)
                    _audio.PlayPvs(ent.Comp.SoundHit, ent.Owner, AudioParams.Default.WithVariation(0.125f).WithVolume(-0.125f));
        }
    }

    private void OnCanDragDrop(Entity<VehicleComponent> ent, ref CanDropTargetEvent args)
    {
        args.CanDrop = !ent.Comp.isBroken;
        args.Handled = true;
    }

    private void OnExamine(Entity<VehicleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if(ent.Comp.isBroken)
            args.PushMarkup(Loc.GetString("vehicle-examine-broken"));
    }
}
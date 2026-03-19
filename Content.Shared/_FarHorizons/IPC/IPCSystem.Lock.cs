using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Audio;
using Content.Shared.DoAfter;
using Content.Shared.Lock;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Utility;

namespace Content.Shared._FarHorizons.Silicons.IPC;

public abstract partial class SharedIPCSystem
{
    protected virtual void SetupLock()
    {
        SubscribeLocalEvent<IPCLockComponent, ComponentStartup>(OnLockStartup);
        SubscribeLocalEvent<IPCLockComponent, IPCLockDoAfter>(OnDoAfterLock);
        SubscribeLocalEvent<IPCLockComponent, IPCUnlockDoAfter>(OnDoAfterUnlock);
        SubscribeLocalEvent<IPCLockComponent, DoAfterAttemptEvent<IPCLockDoAfter>>(DuringLock);
        SubscribeLocalEvent<IPCLockComponent, DoAfterAttemptEvent<IPCUnlockDoAfter>>(DuringUnlock);
    }

    private void AddLockAltVerbs(GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanComplexInteract ||
            !TryComp<IPCLockComponent>(ev.Target, out var lockComp))
            return;

        AlternativeVerb verb = new()
        {
            Act = IsLocked((ev.Target, lockComp))
                ? () => DelayedUnlock((ev.Target, lockComp), ev.User)
                : () => DelayedLock((ev.Target, lockComp), ev.User),
            Text = Loc.GetString(IsLocked((ev.Target, lockComp)) ? "toggle-lock-verb-unlock" : "toggle-lock-verb-lock"),
            Icon = IsLocked((ev.Target, lockComp))
                ? new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/unlock.svg.192dpi.png"))
                : new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/lock.svg.192dpi.png")),
        };
        ev.Verbs.Add(verb);
    }

    private void OnLockStartup(Entity<IPCLockComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.WiresPanel = EnsureComp<WiresPanelComponent>(ent);
        ent.Comp.Lock = EnsureComp<LockComponent>(ent);
    }

    public static bool IsLocked(Entity<IPCLockComponent> ent) =>
        ent.Comp.Lock.Locked;

    public bool CanBeLocked(Entity<IPCLockComponent?> ent) => 
        Resolve(ent, ref ent.Comp) && 
        !ent.Comp.WiresPanel.Open && 
        !IsLocked(ent!);
    
    public bool CanBeUnlocked(Entity<IPCLockComponent?> ent) => 
        Resolve(ent, ref ent.Comp) && 
        !ent.Comp.WiresPanel.Open && 
        IsLocked(ent!);
    
    public void Lock(Entity<IPCLockComponent?> ent, EntityUid? user = null)
    {
        if (Resolve(ent, ref ent.Comp) && CanBeLocked(ent!))
            _lock.Lock(ent, user, ent.Comp.Lock);
    }

    public void Unlock(Entity<IPCLockComponent?> ent, EntityUid? user = null)
    {
        if (Resolve(ent, ref ent.Comp) && CanBeUnlocked(ent))
            _lock.Unlock(ent, user, ent.Comp.Lock);
    }

    public void DelayedLock(Entity<IPCLockComponent> ent, EntityUid user)
    {
        if (!CanBeLocked((ent, ent.Comp)))
            return;

        if((ent.Comp.InstantSelfLock && ent.Owner == user) ||
            ent.Comp.LockTime == TimeSpan.Zero)
            Lock((ent, ent.Comp), user);
        else
            _doAfter.TryStartDoAfter(
                new DoAfterArgs(EntityManager, user, ent.Comp.LockTime, new IPCLockDoAfter(), ent, ent)
                {
                    BreakOnDamage = true,
                    BreakOnMove = true,
                    NeedHand = true,
                    BreakOnDropItem = false,
                    AttemptFrequency = ent.Comp.LockSoundsEnabled ? AttemptFrequency.EveryTick : AttemptFrequency.Never,
                });
    }
    public void DelayedUnlock(Entity<IPCLockComponent> ent, EntityUid user)
    {   
        if (!IsLocked(ent))
            return;

        if((ent.Comp.InstantSelfUnlock && ent.Owner == user) ||
            ent.Comp.UnlockTime == TimeSpan.Zero)
            Unlock((ent, ent.Comp), user);
        else
            _doAfter.TryStartDoAfter(
                new DoAfterArgs(EntityManager, user, ent.Comp.UnlockTime, new IPCUnlockDoAfter(), ent, ent)
                {
                    BreakOnDamage = true,
                    BreakOnMove = true,
                    NeedHand = true,
                    BreakOnDropItem = false,
                    AttemptFrequency = ent.Comp.LockSoundsEnabled ? AttemptFrequency.EveryTick : AttemptFrequency.Never,
                });
    }

    private void OnDoAfterUnlock(Entity<IPCLockComponent> ent, ref IPCUnlockDoAfter args)
    {
        if (!args.Cancelled)
            Unlock((ent, ent.Comp), args.User);
    }
    private void OnDoAfterLock(Entity<IPCLockComponent> ent, ref IPCLockDoAfter args)
    {
        if (!args.Cancelled)
            Lock((ent, ent.Comp), args.User);
    }

    private void DuringLock(Entity<IPCLockComponent> ent, ref DoAfterAttemptEvent<IPCLockDoAfter> args) => 
        HandleSounds(ent, args.Event.User);
    private void DuringUnlock(Entity<IPCLockComponent> ent, ref DoAfterAttemptEvent<IPCUnlockDoAfter> args) => 
        HandleSounds(ent, args.Event.User);

    private void HandleSounds(Entity<IPCLockComponent> ent, EntityUid user)
    {
        if (!ent.Comp.LockSoundsEnabled ||
            _timing.CurTime < ent.Comp.NextSound)
            return;
        
        ent.Comp.NextSound = _timing.CurTime + ent.Comp.LockSoundsCooldown;
        PlayRandomSound(ent, user);
    }

    private void PlayRandomSound(Entity<IPCLockComponent> ent, EntityUid user)
    {
        var shift = _random.Next(12);
        var soundParams = AudioHelpers.ShiftSemitone(ent.Comp.LockSound.Params, shift).AddVolume(-5);

        _audio.PlayPredicted(ent.Comp.LockSound, ent, user, soundParams);
    }
}
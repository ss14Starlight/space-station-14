using Content.Shared.Interaction;
using Content.Shared.Paper;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Paper;

public abstract partial class SharedMultistampSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MultistampComponent, MapInitEvent>(OnMultistampStartup);
        SubscribeLocalEvent<MultistampComponent, ActivateInWorldEvent>(OnMultistampActivated);
        SubscribeLocalEvent<MultistampComponent, AfterAutoHandleStateEvent>(OnMultistampHandleState);

        SubscribeLocalEvent<MultistampComponent, EntInsertedIntoContainerMessage>(OnStampInserted);
        SubscribeLocalEvent<MultistampComponent, EntRemovedFromContainerMessage>(OnStampRemoved);
    }

    private void OnMultistampHandleState(EntityUid uid, MultistampComponent component, ref AfterAutoHandleStateEvent args)
    => SetMultistamp(uid, component);

    private void OnMultistampStartup(EntityUid uid, MultistampComponent stamps, MapInitEvent args)
    {
        // Only set the stamp if we have a stamp component.
        if (TryComp(uid, out StampComponent? stamp))
            SetMultistamp(uid, stamps, stamp);
    }

    private void OnStampInserted(EntityUid uid, MultistampComponent component, ref EntInsertedIntoContainerMessage args)
    {
        component.Stamps.Add(args.Entity);
        SetMultistamp(uid, component, playSound: false);
    }

    private void OnStampRemoved(EntityUid uid, MultistampComponent component, ref EntRemovedFromContainerMessage args)
    {
        if (component.Stamps.Count == 0)
            return;

        if (component.Stamps[component.CurrentEntry] == args.Entity)
        {
            component.Stamps.Remove(args.Entity);
            component.CurrentEntry = !(component.Stamps.Count > component.CurrentEntry) ? component.CurrentEntry : 0;
            Dirty(uid, component);
            SetMultistamp(uid, component, playSound: false);
        }
        else
        {
            component.Stamps.Remove(args.Entity);
            component.CurrentEntry = component.Stamps.Count > 0 ? component.CurrentEntry - 1 : 0;
            CycleMultistamp(uid, component, playSound: false);
        }
    }

    private void OnMultistampActivated(EntityUid uid, MultistampComponent stamps, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        CycleMultistamp(uid, stamps, args.User);
    }

    public bool CycleMultistamp(EntityUid uid, MultistampComponent? stamps = null, EntityUid? user = null, bool playSound = true)
    {
        if (!Resolve(uid, ref stamps))
            return false;

        if (stamps.Stamps.Count == 0)
            return false;

        stamps.CurrentEntry = (stamps.CurrentEntry + 1) % stamps.Stamps.Count;
        Dirty(uid, stamps);
        SetMultistamp(uid, stamps, playSound: playSound, user: user);

        return true;
    }

    public virtual void SetMultistamp(EntityUid uid,
        MultistampComponent? stamps = null,
        StampComponent? stamp = null,
        bool playSound = false,
        EntityUid? user = null)
    {
        if (!Resolve(uid, ref stamps, ref stamp))
            return;

        if (!(stamps.Stamps.Count > stamps.CurrentEntry))
        {
            stamps.CurrentStampName = Loc.GetString("multiple-tool-component-no-behavior");
            Dirty(uid, stamps);
            return;
        }

        if (!TryComp(stamps.Stamps[stamps.CurrentEntry], out StampComponent? current))
            return;

        stamps.CurrentStampName = _entityManager.ToPrettyString(stamps.Stamps[stamps.CurrentEntry]);
        stamp.StampedName = current.StampedName;
        stamp.StampedColor = current.StampedColor;
        stamp.StampState = current.StampState;
        stamp.Sound = current.Sound;

        if (playSound && stamps.ChangeSound != null)
            _audioSystem.PlayPredicted(stamps.ChangeSound, uid, user);

        Dirty(uid, stamps);
    }
}


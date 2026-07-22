using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Overlay.Systems;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared._Starlight.Overlay.Events;
using Content.Client._Starlight.Overlay.Overlays;

namespace Content.Client._Starlight.Overlay.Systems;

public sealed partial class NightVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private TransformSystem _xformSys = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private FlashImmunitySystem _flashImmunity = default!;

    private NightVisionOverlay _overlay = default!;
    [ViewVariables]
    private EntityUid? _effect = null;
    private const string ModernNightVisionShaderPrototype = "ModernNightVisionShader";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NightVisionComponent, ComponentInit>(OnVisionInit);
        SubscribeLocalEvent<NightVisionComponent, ComponentShutdown>(OnVisionShutdown);

        SubscribeLocalEvent<NightVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NightVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<NightVisionComponent, FlashImmunityCheckEvent>(OnFlashImmunityChanged);
        SubscribeLocalEvent<NightVisionComponent, AfterAutoHandleStateEvent>(OnAutoHandleEvent);

        SubscribeLocalEvent<NightVisionBlockerComponent, ComponentInit>(OnBlockerChanged);
        SubscribeLocalEvent<NightVisionBlockerComponent, ComponentRemove>(OnBlockerChanged);

        _overlay = new(_prototypeManager.Index<ShaderPrototype>(ModernNightVisionShaderPrototype));
    }

    private void OnFlashImmunityChanged(Entity<NightVisionComponent> ent, ref FlashImmunityCheckEvent args)
    {
        if (args.IsImmune)
        {
            AttemptRemoveVision(ent.Owner);
        }
        else
        {
            AttemptAddVision(ent.Owner);
        }
    }

    private void OnAutoHandleEvent(Entity<NightVisionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Active)
            AttemptAddVision(ent.Owner);
        else
            AttemptRemoveVision(ent.Owner);
    }

    private void OnPlayerAttached(Entity<NightVisionComponent> ent, ref LocalPlayerAttachedEvent args)
        => AttemptAddVision(ent.Owner);

    private void OnPlayerDetached(Entity<NightVisionComponent> ent, ref LocalPlayerDetachedEvent args)
        => AttemptRemoveVision(ent.Owner, true);

    private void OnVisionInit(Entity<NightVisionComponent> ent, ref ComponentInit args)
        => AttemptAddVision(ent.Owner);

    private void OnVisionShutdown(Entity<NightVisionComponent> ent, ref ComponentShutdown args)
        => AttemptRemoveVision(ent.Owner);

    private void OnBlockerChanged(Entity<NightVisionBlockerComponent> ent, ref ComponentInit args)
        => AttemptRemoveVision(ent.Owner);

    private void OnBlockerChanged(Entity<NightVisionBlockerComponent> ent, ref ComponentRemove args)
        => AttemptAddVision(ent.Owner);

    /// <summary>
    /// Attempt to add the nightvision overlay to the local player.
    /// </summary>
    /// <param name="uid">Entity to add nightvision to.</param>
    private void AttemptAddVision(EntityUid uid)
    {
        if (_player.LocalSession?.AttachedEntity != uid)
            return;

        // if they currently have flash immunity, don't add night vision
        if (_flashImmunity.HasFlashImmunityVisionBlockers(uid))
            return;

        // only add night vision if it's active
        if (!TryComp<NightVisionComponent>(uid, out var nightVision) || !nightVision.Active)
            return;

        // some disabilities (like Nightblind) block night vision
        // some organs, like cyber eyes, can bypass disability - we'll check if the current organ ignores disabilities or not
        if(HasComp<NightVisionBlockerComponent>(uid) && nightVision.DisabilityBlockable)
            return;

        // only add if effect isnt already used
        if (_effect != null)
            return;

        _overlayMan.AddOverlay(_overlay);

        _effect = SpawnAttachedTo(nightVision.EffectPrototype, Transform(uid).Coordinates);
        _xformSys.SetParent(_effect.Value, uid);
    }

    /// <summary>
    /// Attempt to remove the overlay from the local player.
    /// </summary>
    /// <param name="uid">Entity to remove nightvision from.</param>
    /// <param name="force">Use if you need to forcefully remove the overlay no matter what. Only should be used with events that ONLY the local player can fire, like attach/detach</param>
    private void AttemptRemoveVision(EntityUid uid, bool force = false)
    {
        //ENSURE this is the local player
        if (_player.LocalSession?.AttachedEntity != uid && !force) return;

        _overlayMan.RemoveOverlay(_overlay);
        Del(_effect);
        _effect = null;
    }
}

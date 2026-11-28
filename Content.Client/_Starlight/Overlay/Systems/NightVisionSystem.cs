using System.Linq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Client._Starlight.Computers.RemoteControl;
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

    private RemoteControlInterface _remoteControl = default!;
    private NightVisionOverlay _overlay = default!;
    private readonly Dictionary<EntityUid, EntityUid> _effects = [];
    private const string ModernNightVisionShaderPrototype = "ModernNightVisionShader";

    public override void Initialize()
    {
        base.Initialize();

        _remoteControl = EntityManager.System<RemoteControlInterface>();
        _remoteControl.ControlledEntityChanged += OnRemoteControlledEntityChanged;
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

    public override void Shutdown()
    {
        if (_remoteControl is { } remoteControl)
            remoteControl.ControlledEntityChanged -= OnRemoteControlledEntityChanged;
        if (_overlay is { } overlay)
            _overlayMan.RemoveOverlay(overlay);

        foreach (var effect in _effects.Values)
        {
            Del(effect);
        }

        _effects.Clear();
        base.Shutdown();
    }

    private void OnFlashImmunityChanged(Entity<NightVisionComponent> ent, ref FlashImmunityCheckEvent args)
        => UpdateVision(immunityEntity: ent.Owner, immune: args.IsImmune);

    private void OnAutoHandleEvent(Entity<NightVisionComponent> ent, ref AfterAutoHandleStateEvent args)
        => UpdateVision();

    private void OnPlayerAttached(Entity<NightVisionComponent> ent, ref LocalPlayerAttachedEvent args)
        => UpdateVision();

    private void OnPlayerDetached(Entity<NightVisionComponent> ent, ref LocalPlayerDetachedEvent args)
        => UpdateVision(ent.Owner);

    private void OnVisionInit(Entity<NightVisionComponent> ent, ref ComponentInit args)
        => UpdateVision();

    private void OnVisionShutdown(Entity<NightVisionComponent> ent, ref ComponentShutdown args)
        => UpdateVision(ent.Owner);

    private void OnBlockerChanged(Entity<NightVisionBlockerComponent> ent, ref ComponentInit args)
        => UpdateVision();

    private void OnBlockerChanged(Entity<NightVisionBlockerComponent> ent, ref ComponentRemove args)
        => UpdateVision();

    private void OnRemoteControlledEntityChanged(EntityUid? entity)
        => UpdateVision();

    private void UpdateVision(
        EntityUid? excludedEntity = null,
        EntityUid? immunityEntity = null,
        bool? immune = null)
    {
        var viewers = new HashSet<EntityUid>();

        if (_player.LocalSession?.AttachedEntity is { } local
            && local != excludedEntity
            && HasActiveVision(local, immunityEntity, immune))
            viewers.Add(local);

        if (_remoteControl.ControlledEntity is { } remote
            && remote != excludedEntity
            && HasActiveVision(remote, immunityEntity, immune))
            viewers.Add(remote);

        _overlay.SetActiveViewers(viewers);
        if (viewers.Count > 0)
            _overlayMan.AddOverlay(_overlay);
        else
            _overlayMan.RemoveOverlay(_overlay);

        foreach (var viewer in _effects.Keys.Where(viewer => !viewers.Contains(viewer)).ToArray())
        {
            Del(_effects[viewer]);
            _effects.Remove(viewer);
        }

        foreach (var viewer in viewers)
        {
            if (_effects.ContainsKey(viewer) || !TryComp<NightVisionComponent>(viewer, out var nightVision))
                continue;

            var effect = SpawnAttachedTo(nightVision.EffectPrototype, Transform(viewer).Coordinates);
            _xformSys.SetParent(effect, viewer);
            _effects.Add(viewer, effect);
        }
    }

    private bool HasActiveVision(EntityUid uid, EntityUid? immunityEntity, bool? immune)
        => TryComp<NightVisionComponent>(uid, out var vision)
            && vision.Active
            && (uid == immunityEntity ? immune != true : !_flashImmunity.HasFlashImmunityVisionBlockers(uid))
            && (!HasComp<NightVisionBlockerComponent>(uid) || !vision.DisabilityBlockable);
}

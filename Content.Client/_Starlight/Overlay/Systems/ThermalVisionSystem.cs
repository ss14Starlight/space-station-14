using System.Linq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Client._Starlight.Computers.RemoteControl;
using Content.Shared._Starlight.Overlay.Components;
using Content.Client._Starlight.Overlay.Overlays;
using Content.Shared._Starlight.Overlay.Systems;
using Content.Shared._Starlight.Overlay.Events;

namespace Content.Client._Starlight.Overlay.Systems;

public sealed partial class ThermalVisionSystem : SharedThermalVisionSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private TransformSystem _xformSys = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private FlashImmunitySystem _flashImmunity = default!;

    private RemoteControlInterface _remoteControl = default!;
    private ThermalVisionEntityHighlightOverlay _throughWallsOverlay = default!;
    private ThermalVisionOverlay _overlay = default!;

    private const string ThermalVisionShader = "ThermalVisionScreenShader";
    private const string BrightnessShader = "BrightnessShader";
    private const string AlternativeThermalVisionShader = "ThermalVisionScreenShaderHalfAlpha";

    private readonly Dictionary<EntityUid, EntityUid> _effects = [];

    protected override bool IsPredict() => !_timing.IsFirstTimePredicted;

    public override void Initialize()
    {
        base.Initialize();

        _remoteControl = EntityManager.System<RemoteControlInterface>();
        _remoteControl.ControlledEntityChanged += OnRemoteControlledEntityChanged;
        //handled in base class
        //SubscribeLocalEvent<ThermalVisionComponent, ComponentInit>(OnVisionInit);
        //SubscribeLocalEvent<ThermalVisionComponent, ComponentShutdown>(OnVisionShutdown);

        SubscribeLocalEvent<ThermalVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ThermalVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<ThermalVisionComponent, FlashImmunityCheckEvent>(OnFlashImmunityChanged);
        SubscribeLocalEvent<ThermalVisionComponent, AfterAutoHandleStateEvent>(OnAutoHandleEvent);

        _throughWallsOverlay = new(_prototypeManager.Index<ShaderPrototype>(BrightnessShader));
        _overlay = new(
            _prototypeManager.Index<ShaderPrototype>(ThermalVisionShader),
            _prototypeManager.Index<ShaderPrototype>(AlternativeThermalVisionShader));
    }

    public override void Shutdown()
    {
        if (_remoteControl is { } remoteControl)
            remoteControl.ControlledEntityChanged -= OnRemoteControlledEntityChanged;
        if (_throughWallsOverlay is { } throughWallsOverlay)
            _overlayMan.RemoveOverlay(throughWallsOverlay);
        if (_overlay is { } overlay)
            _overlayMan.RemoveOverlay(overlay);

        foreach (var effect in _effects.Values)
        {
            Del(effect);
        }

        _effects.Clear();
        base.Shutdown();
    }

    private void OnFlashImmunityChanged(Entity<ThermalVisionComponent> ent, ref FlashImmunityCheckEvent args)
        => UpdateVision(immunityEntity: ent.Owner, immune: args.IsImmune);

    private void OnAutoHandleEvent(Entity<ThermalVisionComponent> ent, ref AfterAutoHandleStateEvent args)
        => UpdateVision();

    private void OnPlayerAttached(Entity<ThermalVisionComponent> ent, ref LocalPlayerAttachedEvent args)
        => UpdateVision();

    private void OnPlayerDetached(Entity<ThermalVisionComponent> ent, ref LocalPlayerDetachedEvent args)
        => UpdateVision(ent.Owner);

    protected override void ToggleOn(Entity<ThermalVisionComponent> ent)
        => UpdateVision();

    protected override void ToggleOff(Entity<ThermalVisionComponent> ent)
        => UpdateVision(ent.Owner);

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
        _throughWallsOverlay.SetActiveViewers(viewers);

        if (viewers.Count > 0)
        {
            _overlayMan.AddOverlay(_throughWallsOverlay);
            _overlayMan.AddOverlay(_overlay);
        }
        else
        {
            _overlayMan.RemoveOverlay(_throughWallsOverlay);
            _overlayMan.RemoveOverlay(_overlay);
        }

        foreach (var viewer in _effects.Keys.Where(viewer => !viewers.Contains(viewer)).ToArray())
        {
            Del(_effects[viewer]);
            _effects.Remove(viewer);
        }

        foreach (var viewer in viewers)
        {
            if (_effects.ContainsKey(viewer) || !TryComp<ThermalVisionComponent>(viewer, out var thermalVision))
                continue;

            var effect = SpawnAttachedTo(thermalVision.EffectPrototype, Transform(viewer).Coordinates);
            _xformSys.SetParent(effect, viewer);
            _effects.Add(viewer, effect);
        }
    }

    private bool HasActiveVision(EntityUid uid, EntityUid? immunityEntity, bool? immune)
        => TryComp<ThermalVisionComponent>(uid, out var vision)
            && vision.Active
            && (uid == immunityEntity ? immune != true : !_flashImmunity.HasFlashImmunityVisionBlockers(uid));
}

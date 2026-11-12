using Content.Client._Starlight.Overlay.Overlays;
using Content.Shared._Starlight.Overlay.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Overlay.Systems;

public sealed partial class XenoVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    private XenoVisionOverlay _overlay = default!;
    private const string XenoShaderPrototype = "XenoShader";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoVisionComponent, ComponentInit>(OnVisionInit);
        SubscribeLocalEvent<XenoVisionComponent, ComponentShutdown>(OnVisionShutdown);

        SubscribeLocalEvent<XenoVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<XenoVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new(_prototypeManager.Index<ShaderPrototype>(XenoShaderPrototype));
    }

    private void OnPlayerAttached(Entity<XenoVisionComponent> ent, ref LocalPlayerAttachedEvent args)
        => AttemptAddVision(ent.Owner);

    private void OnPlayerDetached(Entity<XenoVisionComponent> ent, ref LocalPlayerDetachedEvent args)
        => AttemptRemoveVision(ent.Owner, true);

    private void OnVisionInit(Entity<XenoVisionComponent> ent, ref ComponentInit args)
        => AttemptAddVision(ent.Owner);

    private void OnVisionShutdown(Entity<XenoVisionComponent> ent, ref ComponentShutdown args)
        => AttemptRemoveVision(ent.Owner);

    private void AttemptAddVision(EntityUid uid)
    {
        //ENSURE this is the local player
        if (_player.LocalSession?.AttachedEntity != uid) return;

        //only add if its active
        if (!TryComp<XenoVisionComponent>(uid, out var xenoVision) || !xenoVision.Active) return;

        _overlayMan.AddOverlay(_overlay);
    }

    /// <summary>
    /// Attempt to remove the overlay from the local player.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="force">Use if you need to forcefully remove the overlay no matter what. Only should be used with events that ONLY the local player can fire, like attach/detach</param>
    private void AttemptRemoveVision(EntityUid uid, bool force = false)
    {
        //ENSURE this is the local player
        if (_player.LocalSession?.AttachedEntity != uid && !force) return;

        _overlayMan.RemoveOverlay(_overlay);
    }
}

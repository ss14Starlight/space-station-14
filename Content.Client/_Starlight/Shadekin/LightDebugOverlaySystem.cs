using Content.Shared._Starlight.Shadekin;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;

namespace Content.Client._Starlight.Shadekin;

internal sealed class LightDebugOverlaySystem : SharedLightDebugOverlaySystem
{
    public readonly Dictionary<EntityUid, LightDebugOverlayMessage> TileData = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RoundRestartCleanupEvent>(Reset);
        SubscribeNetworkEvent<LightDebugOverlayMessage>(HandleMessage);
        SubscribeNetworkEvent<LightDebugOverlayDisableMessage>(HandleDisable);

        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);

        var overlayManager = IoCManager.Resolve<IOverlayManager>();
        if (!overlayManager.HasOverlay<LightDebugOverlay>())
            overlayManager.AddOverlay(new LightDebugOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        var overlayManager = IoCManager.Resolve<IOverlayManager>();
        if (overlayManager.HasOverlay<LightDebugOverlay>())
            overlayManager.RemoveOverlay<LightDebugOverlay>();
    }

    private void OnGridRemoved(GridRemovalEvent ev)
    {
        TileData.Remove(ev.EntityUid);
    }

    private void HandleMessage(LightDebugOverlayMessage message)
    {
        TileData[GetEntity(message.GridId)] = message;
    }

    private void HandleDisable(LightDebugOverlayDisableMessage ev)
    {
        TileData.Clear();
    }

    private void Reset(RoundRestartCleanupEvent ev)
    {
        TileData.Clear();
    }

    public bool HasData(EntityUid gridId)
    {
        return TileData.ContainsKey(gridId);
    }
}

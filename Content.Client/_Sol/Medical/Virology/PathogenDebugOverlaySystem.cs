using Content.Client._Sol.Medical.Virology.Overlays;
using Content.Shared._Sol.Medical.Virology;
using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;

namespace Content.Client._Sol.Medical.Virology;

public sealed class PathogenDebugOverlaySystem : SharedPathogenDebugOverlaySystem
{
    public readonly Dictionary<EntityUid, PathogenDebugOverlayMessage> TileData = new();

    public PathogenDebugOverlayMode CfgMode = PathogenDebugOverlayMode.TotalLoad;
    public float CfgBase;
    public float CfgScale = 10f;
    public string? CfgSpecificPathogen;
    public bool CfgCBM;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(Reset);
        SubscribeNetworkEvent<PathogenDebugOverlayMessage>(HandleMessage);
        SubscribeNetworkEvent<PathogenDebugOverlayDisableMessage>(_ => TileData.Clear());
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);

        var overlays = IoCManager.Resolve<IOverlayManager>();
        if (!overlays.HasOverlay<PathogenDebugOverlay>())
            overlays.AddOverlay(new PathogenDebugOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        var overlays = IoCManager.Resolve<IOverlayManager>();
        if (overlays.HasOverlay<PathogenDebugOverlay>())
            overlays.RemoveOverlay<PathogenDebugOverlay>();
    }

    private void OnGridRemoved(GridRemovalEvent ev) => TileData.Remove(ev.EntityUid);

    private void HandleMessage(PathogenDebugOverlayMessage message)
    {
        TileData[GetEntity(message.GridId)] = message;
    }

    private void Reset(RoundRestartCleanupEvent ev) => TileData.Clear();
}

public enum PathogenDebugOverlayMode : byte
{
    TotalLoad,
    SpecificPathogen,
}

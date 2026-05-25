using Content.Client.SubFloor;
using Content.Shared.VentCrawl;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.VentCrawl;

public sealed partial class VentCrawlSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SubFloorHideSystem _subFloorHideSystem = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    private VentCrawPipeOverlay? _pipeOverlay;

    public override void Initialize()
    {
        base.Initialize();

        _pipeOverlay = new VentCrawPipeOverlay();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var player = _player.LocalSession?.AttachedEntity;

        var inTube = TryComp<VentCrawlerComponent>(player, out var ventCrawler) && ventCrawler.InTube;

        if (_pipeOverlay != null && _overlayManager.HasOverlay<VentCrawPipeOverlay>() != inTube)
        {
            if (inTube)
                _overlayManager.AddOverlay(_pipeOverlay);
            else
                _overlayManager.RemoveOverlay(_pipeOverlay);
        }

        if (_subFloorHideSystem.ShowVentPipe != inTube)
            _subFloorHideSystem.ShowVentPipe = inTube;
    }
}

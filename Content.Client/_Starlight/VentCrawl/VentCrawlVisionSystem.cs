using Content.Client.SubFloor;
using Content.Shared.VentCrawl;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.VentCrawl;

public sealed partial class VentCrawlSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
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

        if (!_timing.IsFirstTimePredicted)
            return;

        var player = _player.LocalSession?.AttachedEntity;

        if (!TryComp<VentCrawlerComponent>(player, out var playerVentCrawlerComponent))
        {
            _subFloorHideSystem.ShowVentPipe = false;
            return;
        }

        var inTube = playerVentCrawlerComponent.InTube;
        _subFloorHideSystem.ShowVentPipe = playerVentCrawlerComponent.InTube;
        if (_pipeOverlay != null && _overlayManager.HasOverlay<VentCrawPipeOverlay>() != inTube)
        {
            if (inTube)
                _overlayManager.AddOverlay(_pipeOverlay);
            else
                _overlayManager.RemoveOverlay(_pipeOverlay);
        }
    }
}

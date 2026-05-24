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

        var ventCraslerQuery = GetEntityQuery<VentCrawlerComponent>();

        if (!ventCraslerQuery.TryGetComponent(player, out var playerVentCrawlerComponent))
        {
            _subFloorHideSystem.ShowVentPipe = false;
            if (_pipeOverlay != null)
                _overlayManager.RemoveOverlay(_pipeOverlay);
            return;
        }

        _subFloorHideSystem.ShowVentPipe = playerVentCrawlerComponent.InTube;
        if (playerVentCrawlerComponent.InTube && _pipeOverlay != null)
            _overlayManager.AddOverlay(_pipeOverlay);
    }
}

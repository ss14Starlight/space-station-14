using Content.Client._Starlight.Silicons.StationAi;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Player;

namespace Content.Client.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    private StationAiWarpUiController? _warpUi;

    private void InitializeWarp()
    {
        _warpUi = new StationAiWarpUiController();
        SubscribeNetworkEvent<StationAiWarpTargetsEvent>(OnWarpTargets);
    }

    private void ShutdownWarp()
    {
        _warpUi?.Dispose();
        _warpUi = null;
    }

    protected override void OnOpenWarpAction(Entity<StationAiHeldComponent> ent, ref StationAiOpenWarpActionEvent args)
    {
        base.OnOpenWarpAction(ent, ref args);

        if (_player.LocalEntity != ent.Owner || _warpUi == null)
            return;

        _warpUi.EnsureWindow(OnWarpTargetSelected, OnWarpWindowClosed);
        _warpUi.SetLoading(true);
        RaiseNetworkEvent(new StationAiWarpRequestEvent());
    }

    private void OnWarpTargets(StationAiWarpTargetsEvent msg, EntitySessionEventArgs args)
    {
        if (_warpUi == null || _player.LocalEntity is not { } local || !HasComp<StationAiHeldComponent>(local))
            return;

        _warpUi.EnsureWindow(OnWarpTargetSelected, OnWarpWindowClosed);
        _warpUi.SetTargets(msg.Targets);
    }

    private void OnWarpTargetSelected(StationAiWarpTarget target)
    {
        RaiseNetworkEvent(new StationAiWarpToTargetEvent(target.Target));
        _warpUi?.CloseWindow();
    }

    private void OnWarpWindowClosed()
    {
        _warpUi?.ClearWindow();
    }
}

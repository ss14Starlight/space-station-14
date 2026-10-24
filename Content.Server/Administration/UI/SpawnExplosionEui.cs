using Content.Server._Starlight.Administration.Systems;
using Content.Server.EUI;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Server.Administration.UI;

/// <summary>
///     Admin Eui for spawning and preview-ing explosions
/// </summary>
[UsedImplicitly]
public sealed class SpawnExplosionEui : BaseEui
{
    private readonly ExplosionSystem _explosionSystem;
    private readonly ISawmill _sawmill;

    public SpawnExplosionEui()
    {
        _explosionSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ExplosionSystem>();
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("explosion");
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not SpawnExplosionEuiMsg.PreviewRequest request)
            return;

        if (request.TotalIntensity <= 0 || request.IntensitySlope <= 0)
            return;

        // Starlight-start: the preview runs the same tile fill as a real explosion, so it gets the same caps.
        var total = request.TotalIntensity;
        var slope = request.IntensitySlope;
        var maxTile = request.MaxIntensity;
        if (IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AdminLimitSystem>().ClampExplosion(Player, ref total, ref slope, ref maxTile))
            request = new SpawnExplosionEuiMsg.PreviewRequest(request.Epicenter, request.TypeId, total, slope, maxTile);
        // Starlight-end

        var explosion = _explosionSystem.GenerateExplosionPreview(request);

        if (explosion == null)
        {
            _sawmill.Error("Failed to generate explosion preview.");
            return;
        }

        SendMessage(new SpawnExplosionEuiMsg.PreviewData(explosion, request.IntensitySlope, request.TotalIntensity));
    }
}

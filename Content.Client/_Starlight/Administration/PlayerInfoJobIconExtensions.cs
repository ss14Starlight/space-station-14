using System.Diagnostics.CodeAnalysis;
using Content.Shared.Administration;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Administration;

/// <summary>
/// Helper class for retrieving the job icon texture for a given player.
/// </summary>
public static class PlayerInfoJobIconExtensions
{
    /// <summary>
    /// Attempts to retrieve the job icon texture for a given player info. Returns true if successful, false otherwise.
    /// </summary>
    public static bool TryGetJobIconTexture(this PlayerInfo info, [NotNullWhen(true)] out Texture? texture)
    {
        texture = null;
        if (info.JobId is not { } jobId)
            return false;

        var proto = IoCManager.Resolve<IPrototypeManager>();
        if (!proto.TryIndex(jobId, out var job) || !proto.TryIndex(job.Icon, out var icon))
            return false;

        if (!IoCManager.Resolve<IEntitySystemManager>().TryGetEntitySystem<SpriteSystem>(out var sprite))
            return false;

        texture = sprite.Frame0(icon.Icon);
        return true;
    }
}

using Content.Server.Administration.Managers;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Administration.Systems;

public sealed partial class AdminLimitSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextTippy;
    private TimeSpan _nextGlobalSound;
    private readonly Dictionary<NetUserId, TimeSpan> _nextExplosion = new();

    public bool IsExempt(ICommonSession? admin)
        => admin == null || _admin.HasAdminFlag(admin, AdminFlags.Host);

    public bool TryUseTippy(ICommonSession? admin, out float remaining)
        => TryUseShared(admin, ref _nextTippy, StarlightCCVars.AdminTippyCooldown, out remaining);

    public bool TryUseGlobalSound(ICommonSession? admin, out float remaining)
        => TryUseShared(admin, ref _nextGlobalSound, StarlightCCVars.AdminGlobalSoundCooldown, out remaining);

    public bool TryUseExplosion(ICommonSession? admin, out float remaining)
    {
        remaining = 0f;
        if (IsExempt(admin))
            return true;

        var now = _timing.CurTime;
        if (_nextExplosion.TryGetValue(admin!.UserId, out var next) && next > now)
        {
            remaining = (float) (next - now).TotalSeconds;
            return false;
        }

        _nextExplosion[admin.UserId] = now + TimeSpan.FromSeconds(_cfg.GetCVar(StarlightCCVars.AdminExplosionCooldown));
        return true;
    }

    public bool ClampExplosion(ICommonSession? admin, ref float totalIntensity, ref float slope, ref float maxTileIntensity)
    {
        // "NaN" and "Infinity" parse as floats and slip past the explosion system's "<= 0" checks, even for hosts.
        var invalid = !float.IsFinite(totalIntensity) || !float.IsFinite(slope) || !float.IsFinite(maxTileIntensity);
        if (invalid)
        {
            totalIntensity = float.IsFinite(totalIntensity) ? totalIntensity : 0f;
            slope = float.IsFinite(slope) ? slope : 0f;
            maxTileIntensity = float.IsFinite(maxTileIntensity) ? maxTileIntensity : 0f;
        }

        if (IsExempt(admin))
            return invalid;

        var maxTotal = _cfg.GetCVar(StarlightCCVars.AdminExplosionMaxIntensity);
        var maxTile = _cfg.GetCVar(StarlightCCVars.AdminExplosionMaxTileIntensity);
        var clamped = invalid || totalIntensity > maxTotal || maxTileIntensity > maxTile;

        totalIntensity = MathF.Min(totalIntensity, maxTotal);
        maxTileIntensity = MathF.Min(maxTileIntensity, maxTile);
        return clamped;
    }

    public float ClampGlobalSoundVolume(ICommonSession? admin, float volume)
        => IsExempt(admin) ? volume : MathF.Min(volume, _cfg.GetCVar(StarlightCCVars.AdminGlobalSoundMaxVolume));

    private bool TryUseShared(ICommonSession? admin, ref TimeSpan next, CVarDef<float> cooldown, out float remaining)
    {
        remaining = 0f;
        if (IsExempt(admin))
            return true;

        var now = _timing.CurTime;
        if (next > now)
        {
            remaining = (float) (next - now).TotalSeconds;
            return false;
        }

        next = now + TimeSpan.FromSeconds(_cfg.GetCVar(cooldown));
        return true;
    }
}

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    #region Player data
    /*
     * Player data 🌟Starlight🌟
     */
    public async Task<StarLightModel.PlayerDataDTO?> GetPlayerDataDTOForAsync(NetUserId userId, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.PlayerData
            .SingleOrDefaultAsync(p => p.UserId == userId.UserId, cancel);
    }
    public async Task SetPlayerDataForAsync(NetUserId userId, StarLightModel.PlayerDataDTO data, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var obj = await db.DbContext.PlayerData
            .SingleOrDefaultAsync(p => p.UserId == userId.UserId, cancel);
        if (obj != null)
        {
            obj.Balance = data.Balance;
            obj.GhostTheme = data.GhostTheme;
        }
        else
            db.DbContext.PlayerData.Add(data);

        await db.DbContext.SaveChangesAsync();
    }
    #endregion

    #region PendingPreferenceData

    public async Task<StarLightModel.PendingPreference?> GetPendingPreferenceAsync(NetUserId userId, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.PendingPreference
            .Include(p => p.Profiles)
            .Include(p => p.JobPriorities)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId.UserId, cancel);
    }

    public async Task SavePendingProfileAsync(NetUserId userId, NetUserId adminId, string adminName, int slot, string? data)
    {
        await using var db = await GetDb();
        var pending = await GetOrCreatePendingPreference(db.DbContext, userId, adminId, adminName);

        var existing = pending.Profiles.SingleOrDefault(p => p.Slot == slot);
        if (existing is null)
            pending.Profiles.Add(new StarLightModel.PendingProfile { Slot = slot, Data = data });
        else
            existing.Data = data;

        await db.DbContext.SaveChangesAsync();
    }

    public async Task SavePendingJobPrioritiesAsync(NetUserId userId, NetUserId adminId, string adminName,
        Dictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities)
    {
        await using var db = await GetDb();
        var pending = await GetOrCreatePendingPreference(db.DbContext, userId, adminId, adminName);

        pending.HasJobPriorities = true;
        pending.JobPriorities =
        [
            .. jobPriorities
                .Select(p =>
                    new StarLightModel.PendingJobPriority { JobName = p.Key, Priority = (DbJobPriority)p.Value })
        ];

        await db.DbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes the pending preferences for a player, but only if they are still at the given revision.
    /// </summary>
    /// <returns>False if the pending preferences were modified since <paramref name="revision"/> and were not deleted.</returns>
    public async Task<bool> DeletePendingPreferenceAsync(NetUserId userId, int revision)
    {
        await using var db = await GetDb();
        var pending = await db.DbContext.PendingPreference
            .Include(p => p.Profiles)
            .Include(p => p.JobPriorities)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.UserId == userId.UserId);

        if (pending is null)
            return true;
        if (pending.Revision != revision)
            return false;

        db.DbContext.PendingPreference.Remove(pending);
        await db.DbContext.SaveChangesAsync();
        return true;
    }

    private static async Task<StarLightModel.PendingPreference> GetOrCreatePendingPreference(ServerDbContext db,
        NetUserId userId, NetUserId adminId, string adminName)
    {
        var pending = await db.PendingPreference
            .Include(p => p.Profiles)
            .Include(p => p.JobPriorities)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.UserId == userId.UserId);

        if (pending is null)
        {
            pending = new StarLightModel.PendingPreference { UserId = userId.UserId };
            db.PendingPreference.Add(pending);
        }

        pending.LastAdminUserId = adminId.UserId;
        pending.LastAdminName = adminName;
        pending.Revision++;
        return pending;
    }

    #endregion
}

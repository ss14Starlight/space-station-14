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

    // public async Task SavePendingCharacterSlotAsync(NetUserId userId, HumanoidCharacterProfile? humanoid, int slot)
    // {
    //     await using var db = await GetDb();
    //
    //     if (humanoid is null)
    //     {
    //         await DeletePendingCharacterSlot(db.DbContext, userId, slot);
    //         await db.DbContext.SaveChangesAsync();
    //         return;
    //     }
    //
    //     var oldProfile = db.DbContext.Profile
    //         .Include(p => p.StarLightProfile) // Starlight
    //         .Include(p => p.Preference)
    //         .Where(p => p.Preference.UserId == userId.UserId)
    //         .Include(p => p.Jobs)
    //         .Include(p => p.Antags)
    //         .Include(p => p.Traits)
    //         .Include(p => p.Loadouts)
    //         .ThenInclude(l => l.Groups)
    //         .ThenInclude(group => group.Loadouts)
    //         .Include(p => p.CharacterInfo) // Starlight-edit
    //         // Cosmatic Drift Record System-start: Pull forward existing CD profile state when editing a saved slot
    //         .Include(p => p.CDProfile)
    //         // Entity Framework will populate CharacterRecordEntries for any existing CDProfile,
    //         // so it's safe to suppress the nullable warning here as well.
    //         .ThenInclude(cdProfile => cdProfile!.CharacterRecordEntries)
    //         // Cosmatic Drift Record System-end
    //         .AsSplitQuery()
    //         .SingleOrDefault(h => h.Slot == slot);
    //
    //     var newProfile = ConvertProfiles(humanoid, slot, oldProfile);
    //     if (oldProfile == null)
    //     {
    //         var prefs = await db.DbContext
    //             .PendingPreference
    //             .Include(p => p.Profiles)
    //             .SingleAsync(p => p.UserId == userId.UserId);
    //
    //         prefs.Profiles.Add(newProfile);
    //     }
    //
    //     await db.DbContext.SaveChangesAsync();
    // }
    //
    // public async Task SavePendingJobPrioritiesAsync(NetUserId userId,
    //     Dictionary<ProtoId<JobPrototype>, JobPriority> newJobPriorities)
    // {
    //     await using var db = await GetDb();
    //
    //     var playerPreference = db.DbContext.PendingPreference
    //         .Include(p => p.JobPriorities)
    //         .Single(p => p.UserId == userId.UserId);
    //
    //     var newPrios = new List<JobPriorityEntry>();
    //     foreach (var (job, priority) in newJobPriorities)
    //     {
    //         var newPrio = new JobPriorityEntry { JobName = job, Priority = (DbJobPriority)priority, };
    //         newPrios.Add(newPrio);
    //     }
    //
    //     playerPreference.JobPriorities = newPrios;
    //
    //     await db.DbContext.SaveChangesAsync();
    // }
    //
    // private static async Task DeletePendingCharacterSlot(ServerDbContext db, NetUserId userId, int slot)
    // {
    //     var profile = await db.Profile.Include(p => p.Preference)
    //         .Where(p => p.Preference.UserId == userId.UserId && p.Slot == slot)
    //         .SingleOrDefaultAsync();
    //
    //     if (profile == null)
    //     {
    //         return;
    //     }
    //
    //     db.Profile.Remove(profile);
    // }

    #endregion
}

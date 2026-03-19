using System.Linq;
using Content.Server.Database;
using Content.Shared._NullLink;
using Content.Shared.NullLink.CCVar;
using Robust.Shared.Network;

namespace Content.Server._NullLink.PlayerData;

public sealed partial class NullLinkPlayerManager
{
    private sealed record AdminRankSnapshot(string Name, ulong[] Roles, string[] Flags);

    private string? _adminBuilderId;
    private List<AdminRankSnapshot>? _adminRanksSnapshot;
    private Dictionary<string, int>? _adminRankIds;

    private void UpdateAdminBuilder(string obj)
    {
        if (_adminBuilderId == obj)
            return;

        if (string.IsNullOrEmpty(obj) || !_proto.TryIndex<AdminRankBuilderPrototype>(obj, out var builder))
        {
            _adminBuilderId = null;
            _adminRanksSnapshot = null;
            _adminRankIds = null;
            return;
        }

        // Deep-copy: after this point the prototype object is never referenced again.
        // loadprototype / MsgReloadPrototypes can replace it in memory, we don't care.
        _adminBuilderId = obj;
        _adminRanksSnapshot = [.. builder.Ranks.Select(r => new AdminRankSnapshot(r.Name, [.. r.Roles], [.. r.Flags]))];
        _adminRankIds = null;
        EnsureAdminRanks();
    }

    private async void EnsureAdminRanks()
    {
        if (_adminRanksSnapshot == null)
            return;

        try
        {
            var (_, existingRanks) = await _dbManager.GetAllAdminAndRanksAsync();
            var rankIds = new Dictionary<string, int>();

            foreach (var entry in _adminRanksSnapshot)
            {
                var existing = existingRanks.FirstOrDefault(r => r.Name == entry.Name);
                if (existing != null)
                {
                    var existingFlags = existing.Flags.Select(f => f.Flag).OrderBy(f => f).ToArray();
                    var newFlags = entry.Flags.OrderBy(f => f).ToArray();
                    if (!existingFlags.SequenceEqual(newFlags))
                    {
                        var updateRank = new AdminRank
                        {
                            Id = existing.Id,
                            Name = entry.Name,
                            Flags = entry.Flags.Select(f => new AdminRankFlag { Flag = f }).ToList(),
                        };
                        await _dbManager.UpdateAdminRankAsync(updateRank);
                    }
                    rankIds[entry.Name] = existing.Id;
                }
                else
                {
                    var dbRank = new AdminRank
                    {
                        Name = entry.Name,
                        Flags = entry.Flags.Select(f => new AdminRankFlag { Flag = f }).ToList(),
                    };
                    await _dbManager.AddAdminRankAsync(dbRank);
                    rankIds[entry.Name] = dbRank.Id;
                }
            }

            _adminRankIds = rankIds;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"EnsureAdminRanks failed: {ex}");
        }
    }

    private async void AdminCheck(Guid playerId, PlayerData playerData)
    {
        if (_adminRanksSnapshot == null || _adminRankIds == null)
            return;

        try
        {
            var netUserId = new NetUserId(playerId);
            int? matchedRankId = null;
            string? matchedName = null;

            foreach (var entry in _adminRanksSnapshot)
            {
                if (entry.Roles.All(playerData.Roles.Contains))
                {
                    if (_adminRankIds.TryGetValue(entry.Name, out var rankId))
                    {
                        matchedRankId = rankId;
                        matchedName = entry.Name;
                    }
                    break;
                }
            }

            if (matchedRankId != null)
            {
                var existing = await _dbManager.GetAdminDataForAsync(netUserId);
                if (existing == null)
                {
                    var admin = new Admin
                    {
                        UserId = playerId,
                        AdminRankId = matchedRankId,
                        Title = matchedName,
                        Flags = [],
                    };
                    await _dbManager.AddAdminAsync(admin);
                }
                else if (_adminRankIds.ContainsValue(existing.AdminRankId ?? -1) && existing.AdminRankId != matchedRankId)
                {
                    existing.AdminRankId = matchedRankId;
                    existing.Title = matchedName;
                    await _dbManager.UpdateAdminAsync(existing);
                }
                _adminManager.ReloadAdmin(playerData.Session);
            }
            else
            {
                var existing = await _dbManager.GetAdminDataForAsync(netUserId);
                if (existing != null && _adminRankIds.ContainsValue(existing.AdminRankId ?? -1))
                {
                    await _dbManager.RemoveAdminAsync(netUserId);
                    _adminManager.ReloadAdmin(playerData.Session);
                }
            }
        }
        catch (Exception ex)
        {
            _sawmill.Error($"AdminCheck failed for {playerId}: {ex}");
        }
    }
}

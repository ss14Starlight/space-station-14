using System.Threading.Tasks;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    private readonly Dictionary<NetUserId, JobPrioritiesSave> _jobPrioritiesSaves = new();

    private sealed class JobPrioritiesSave
    {
        public Dictionary<ProtoId<JobPrototype>, JobPriority>? Pending;
    }

    private async Task SaveJobPrioritiesQueued(NetUserId userId, Dictionary<ProtoId<JobPrototype>, JobPriority> priorities)
    {
        if (_jobPrioritiesSaves.TryGetValue(userId, out var running))
        {
            running.Pending = priorities;
            return;
        }

        var save = new JobPrioritiesSave();
        _jobPrioritiesSaves[userId] = save;
        try
        {
            var next = priorities;
            while (next != null)
            {
                save.Pending = null;
                try
                {
                    await _db.SaveJobPrioritiesAsync(userId, next);
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Failed to save job priorities of {userId}: {e}");
                }

                next = save.Pending;
            }
        }
        finally
        {
            _jobPrioritiesSaves.Remove(userId);
        }
    }
}

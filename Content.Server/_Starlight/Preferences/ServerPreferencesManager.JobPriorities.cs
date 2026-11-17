using System.Threading.Tasks;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    /// <summary>
    /// Players whose job priorities are being saved right now.
    /// </summary>
    private readonly Dictionary<NetUserId, JobPrioritiesSave> _jobPrioritiesSaves = new();

    private sealed class JobPrioritiesSave
    {
        /// <summary>
        /// Priorities that came in while a save was running, to be saved once it finishes.
        /// Only the latest ones matter, so each new message overwrites the previous one.
        /// </summary>
        public Dictionary<ProtoId<JobPrototype>, JobPriority>? Pending;
    }

    private async Task SaveJobPrioritiesQueued(NetUserId userId, Dictionary<ProtoId<JobPrototype>, JobPriority> priorities)
    {
        // A save is already running: leave the new priorities for it to pick up when it's done.
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
                // Clear before awaiting: the messages that arrive during the await set Pending again (see above).
                // Everything runs on the main thread, so nothing changes it between here and the await.
                save.Pending = null;
                try
                {
                    await _db.SaveJobPrioritiesAsync(userId, next);
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Failed to save job priorities of {userId}: {e}");
                }

                // Null when nothing came in during the save, which ends the loop.
                // Otherwise these are the latest priorities, and any skipped in between are outdated anyway.
                next = save.Pending;
            }
        }
        finally
        {
            _jobPrioritiesSaves.Remove(userId);
        }
    }
}

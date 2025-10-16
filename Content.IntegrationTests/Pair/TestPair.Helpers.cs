#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Pair;

// Contains misc helper functions to make writing tests easier.
public sealed partial class TestPair
{
    public Task<TestMapData> CreateTestMap(bool initialized = true)
        => CreateTestMap(initialized, "Plating");

    /// <summary>
    /// Set a user's antag preferences. Modified preferences are automatically reset at the end of the test.
    /// </summary>
    public async Task SetAntagPreference(ProtoId<AntagPrototype> id, bool value, NetUserId? user = null)
    {
        user ??= Client.User!.Value;
        if (user is not { } userId)
            return;

        var prefMan = Server.ResolveDependency<IServerPreferencesManager>();
        var prefs = prefMan.GetPreferences(userId);

        // Automatic preference resetting only resets slot 0.
        //Assert.That(prefs.SelectedCharacterIndex, Is.EqualTo(0)); //Starlight has no concept of "selected character"

        var profile = (HumanoidCharacterProfile)prefs.Characters[0];
        var newProfile = profile.WithAntagPreference(id, value);
        await Server.WaitPost(() => prefMan.SetProfile(userId, 0, newProfile).Wait());
    }

    /// <summary>
    /// Set a user's job preferences.  Modified preferences are automatically reset at the end of the test.
    /// </summary>
    public async Task SetJobPriority(ProtoId<JobPrototype> id, JobPriority value, NetUserId? user = null)
    {
        user ??= Client.User!.Value;
        if (user is { } userId)
            await SetJobPriorities(userId, (id, value));
    }

    /// <inheritdoc cref="SetJobPriority"/>
    public async Task SetJobPriorities(params (ProtoId<JobPrototype>, JobPriority)[] priorities)
        => await SetJobPriorities(Client.User!.Value, priorities);

    /// <inheritdoc cref="SetJobPriority"/>
    public async Task SetJobPriorities(NetUserId user, params (ProtoId<JobPrototype>, JobPriority)[] priorities)
    {
        var highCount = priorities.Count(x => x.Item2 == JobPriority.High);
        Assert.That(highCount, Is.LessThanOrEqualTo(1), "Cannot have more than one high priority job");

        var prefMan = Server.ResolveDependency<IServerPreferencesManager>();
        var prefs = prefMan.GetPreferences(user);
        var profile = (HumanoidCharacterProfile)prefs.Characters[0];
        var dictionary = new Dictionary<ProtoId<JobPrototype>, JobPriority>(prefs.JobPriorities); //Starlight: priorities are on the prefs

        // Automatic preference resetting only resets slot 0.
        //Index, Is.EqualTo(0)); //Starlight has no "selected index"

        if (highCount != 0)
        {
            foreach (var (key, priority) in dictionary)
            {
                if (priority == JobPriority.High)
                    dictionary[key] = JobPriority.Medium;
            }
        }

        foreach (var (job, priority) in priorities)
        {
            if (priority == JobPriority.Never)
                dictionary.Remove(job);
            else
                dictionary[job] = priority;
        }

        //Starlight. Priority is on the prefman.
        await Server.WaitPost(() => prefMan.SetJobPriorities(user, dictionary).Wait());
        //var newProfile = profile.WithJobPriorities(dictionary);
        await Server.WaitPost(() => prefMan.SetProfile(user, 0, profile).Wait());
    }

    #region Starlight
    /// <summary>
    /// Add dummy players to the pair with server saved job priority preferences
    /// </summary>
    /// <param name="jobPriorities">Job priorities to initialize the players with</param>
    /// <param name="count">How many players to add</param>
    /// <returns>Enumerable of sessions for the new players</returns>
    public Task<IEnumerable<ICommonSession>> AddDummyPlayers(Dictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities, int count = 1)
    {
        return AddDummyPlayers(jobPriorities, jobPriorities.Keys, count);
    }

    public async Task<IEnumerable<ICommonSession>> AddDummyPlayers(
        Dictionary<ProtoId<JobPrototype>,JobPriority> jobPriorities,
        IEnumerable<ProtoId<JobPrototype>> jobPreferences,
        int count=1)
    {
        var prefMan = Server.ResolveDependency<IServerPreferencesManager>();
        var dbMan = Server.ResolveDependency<UserDbDataManager>();

        var sessions = await Server.AddDummySessions(count);
        await RunTicksSync(5);
        var tasks = sessions.Select(s =>
        {
            // dbMan.ClientConnected(s);
            dbMan.WaitLoadComplete(s).Wait();
            var newProfile = HumanoidCharacterProfile.Random().WithJobPreferences(jobPreferences).AsEnabled();
            return Task.WhenAll(
                prefMan.SetJobPriorities(s.UserId, jobPriorities),
                prefMan.SetProfile(s.UserId, 0, newProfile));
        });
        await Server.WaitPost(() => Task.WhenAll(tasks).Wait());
        await RunTicksSync(5);

        return sessions;
    }
    #endregion
}

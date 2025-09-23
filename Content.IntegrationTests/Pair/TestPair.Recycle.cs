#nullable enable
using System.IO;
using System.Linq;
using Content.Client.Lobby;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Pair;

// This partial class contains logic related to recycling & disposing test pairs.
public sealed partial class TestPair
{
    protected override async Task Cleanup()
    {
        await base.Cleanup();
        await ResetModifiedPreferences();
    }

    private async Task ResetModifiedPreferences()
    {
        if (Player == null)
            return;

        await ReallyBeIdle();

        // reset through the client so that the client's cached preferences get updated
        var prefMan = Client.ResolveDependency<IClientPreferencesManager>();
        var prefs = prefMan.Preferences;

        await Client.WaitAssertion(() =>
        {
<<<<<<< HEAD
            foreach (var slot in prefs!.Characters.Keys)
            {
                if (slot == 0)
                    continue;
                prefMan.DeleteCharacter(slot);
            }

            prefMan.UpdateCharacter(new HumanoidCharacterProfile().AsEnabled(), 0);
            prefMan.UpdateJobPriorities(new() { { SharedGameTicker.FallbackOverflowJob, JobPriority.High } });
        });

        await ReallyBeIdle();
=======
            await Server.WaitPost(() => prefMan.SetProfile(user, 0, new HumanoidCharacterProfile()).Wait());
        }

        _modifiedProfiles.Clear();
>>>>>>> upstream/master
    }

    protected override async Task Recycle(PairSettings next, TextWriter testOut)
    {
<<<<<<< HEAD
        if (State != PairState.InUse)
            throw new Exception($"{nameof(CleanReturnAsync)}: Unexpected state. Pair: {Id}. State: {State}.");

        await _testOut.WriteLineAsync($"{nameof(CleanReturnAsync)}: Return of pair {Id} started");
        State = PairState.CleanDisposed;
        try
        {
            await OnCleanDispose();
        }
        catch (Exception e)
        {
            await _testOut.WriteLineAsync($"Exception raised in OnCleanDispose\n{e}");
            throw;
        }
        DebugTools.Assert(State is PairState.Dead or PairState.Ready);
        PoolManager.NoCheckReturn(this);
        ClearContext();
    }

    public async ValueTask DisposeAsync()
    {
        switch (State)
        {
            case PairState.Dead:
            case PairState.Ready:
                break;
            case PairState.InUse:
                await _testOut.WriteLineAsync($"{nameof(DisposeAsync)}: Dirty return of pair {Id} started");
                await OnDirtyDispose();
                PoolManager.NoCheckReturn(this);
                ClearContext();
                break;
            default:
                throw new Exception($"{nameof(DisposeAsync)}: Unexpected state. Pair: {Id}. State: {State}.");
        }
    }

    public async Task CleanPooledPair(PoolSettings settings, TextWriter testOut)
    {
        Settings = default!;
        Watch.Restart();
        await testOut.WriteLineAsync($"Recycling...");

        var gameTicker = Server.System<GameTicker>();
        var cNetMgr = Client.ResolveDependency<IClientNetManager>();

        await RunTicksSync(1);

        // Disconnect the client if they are connected.
        if (cNetMgr.IsConnected)
        {
            await testOut.WriteLineAsync($"Recycling: {Watch.Elapsed.TotalMilliseconds} ms: Disconnecting client.");
            await Client.WaitPost(() => cNetMgr.ClientDisconnect("Test pooling cleanup disconnect"));
            await RunTicksSync(1);
        }
        Assert.That(cNetMgr.IsConnected, Is.False);

=======
>>>>>>> upstream/master
        // Move to pre-round lobby. Required to toggle dummy ticker on and off
        var gameTicker = Server.System<GameTicker>();
        if (gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
        {
            await testOut.WriteLineAsync($"Recycling: {Watch.Elapsed.TotalMilliseconds} ms: Restarting round.");
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            Assert.That(gameTicker.DummyTicker, Is.False);
            Server.CfgMan.SetCVar(CCVars.GameLobbyEnabled, true);
            await Server.WaitPost(() => gameTicker.RestartRound());
            await RunTicksSync(1);
        }

        //Apply Cvars
        await testOut.WriteLineAsync($"Recycling: {Watch.Elapsed.TotalMilliseconds} ms: Setting CVar ");
        await ApplySettings(next);
        await RunTicksSync(1);

        // Restart server.
        await testOut.WriteLineAsync($"Recycling: {Watch.Elapsed.TotalMilliseconds} ms: Restarting server again");
        await Server.WaitPost(() => Server.EntMan.FlushEntities());
        await Server.WaitPost(() => gameTicker.RestartRound());
        await RunTicksSync(1);
    }

    public override void ValidateSettings(PairSettings s)
    {
        base.ValidateSettings(s);
        var settings = (PoolSettings) s;

        var cfg = Server.CfgMan;
        Assert.That(cfg.GetCVar(CCVars.AdminLogsEnabled), Is.EqualTo(settings.AdminLogsEnabled));
        Assert.That(cfg.GetCVar(CCVars.GameLobbyEnabled), Is.EqualTo(settings.InLobby));
        Assert.That(cfg.GetCVar(CCVars.GameDummyTicker), Is.EqualTo(settings.DummyTicker));

        var ticker = Server.System<GameTicker>();
        Assert.That(ticker.DummyTicker, Is.EqualTo(settings.DummyTicker));

        var expectPreRound = settings.InLobby | settings.DummyTicker;
        var expectedLevel = expectPreRound ? GameRunLevel.PreRoundLobby : GameRunLevel.InRound;
        Assert.That(ticker.RunLevel, Is.EqualTo(expectedLevel));

        if (ticker.DummyTicker || !settings.Connected)
            return;

        var sPlayer = Server.ResolveDependency<ISharedPlayerManager>();
        var session = sPlayer.Sessions.Single();
        var status = ticker.PlayerGameStatuses[session.UserId];
        var expected = settings.InLobby
            ? PlayerGameStatus.NotReadyToPlay
            : PlayerGameStatus.JoinedGame;

        Assert.That(status, Is.EqualTo(expected));

        if (settings.InLobby)
        {
            Assert.That(session.AttachedEntity, Is.Null);
            return;
        }

        Assert.That(session.AttachedEntity, Is.Not.Null);
        Assert.That(Server.EntMan.EntityExists(session.AttachedEntity));
        Assert.That(Server.EntMan.HasComponent<MindContainerComponent>(session.AttachedEntity));
        var mindCont = Server.EntMan.GetComponent<MindContainerComponent>(session.AttachedEntity!.Value);
        Assert.That(mindCont.Mind, Is.Not.Null);
        Assert.That(Server.EntMan.TryGetComponent(mindCont.Mind, out MindComponent? mind));
        Assert.That(mind!.VisitingEntity, Is.Null);
        Assert.That(mind.OwnedEntity, Is.EqualTo(session.AttachedEntity!.Value));
        Assert.That(mind.UserId, Is.EqualTo(session.UserId));
    }
}

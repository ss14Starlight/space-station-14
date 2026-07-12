using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._NullLink.Core;
using Content.Server._NullLink.Helpers;
using Content.Server._NullLink.PlayerData;
using Starlight.NullLink;
using Starlight.NullLink.Event;

namespace Content.Server._NullLink.EventBus;

public sealed partial class NullLinkEventBusManager : IEventBusObserver, INullLinkEventBusManager
{
    private static readonly TimeSpan _grainDelay = TimeSpan.FromSeconds(180);

    private static readonly TimeSpan[] _resubscribeBackoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
    ];

    private Timer? _resubscribeTimer;
    private int _resubscribeFailures;

    [Dependency] private IActorRouter _actors = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private INullLinkPlayerManager _players = default!;

    private ISawmill _sawmill = default!;
    private readonly ConcurrentQueue<BaseEvent> _eventQueue = [];

    public event Action<AdminNote>? NoteAdded;

    public event Action<AdminNote>? NoteChanged;

    public event Action<AdminNote>? NoteRemoved;

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("NullLink event bus");
        _actors.OnConnected += OnNullLinkConnected;
        _resubscribeTimer = new Timer(
            static state => ((NullLinkEventBusManager)state!).OnResubscribeTimer(),
            this,
            dueTime: TimeSpan.Zero,
            period: Timeout.InfiniteTimeSpan);
    }


    public void Shutdown()
    {
        _resubscribeTimer?.Dispose();
        _actors.OnConnected -= OnNullLinkConnected;

        if (!_actors.Enabled
            || !_actors.TryGetServerGrain(out var serverGrain)
            || !_actors.TryCreateObjectReference<IEventBusObserver>(this, out var eventBusObserver)
            || eventBusObserver is null)
            return;

        serverGrain.UnsubscribeEventBus(eventBusObserver)
            .FireAndForget(ex => _sawmill.Warning($"Failed to unsubscribe from the NullLink event bus on shutdown: {ex}"));
    }

    public bool TryDequeue([MaybeNullWhen(false)] out BaseEvent result)
        => _eventQueue.TryDequeue(out result);

    public ValueTask OnEventReceived<T>(T @event) where T : BaseEvent
        => @event switch
        {
            PlayerRolesSyncEvent playerRolesSyncEvent
                => _players.SyncRoles(playerRolesSyncEvent),

            RolesChangedEvent rolesChangedEvent
                => _players.UpdateRoles(rolesChangedEvent),

            PlayerServerPlayTimesSyncEvent playTimesSync
                => _players.SyncPlayTime(playTimesSync),

            PlayerResourcesSyncEvent playerResourcesSyncEvent
                => _players.SyncResources(playerResourcesSyncEvent),

            ResourceChangedEvent resourceChangedEvent
                => _players.UpdateResource(resourceChangedEvent),

            NotesChangedEvent notesChangedevent
                => ProcessNotes(notesChangedevent),

            BaseEvent baseEvent
                => Enqueue(baseEvent),
        };

    private ValueTask ProcessNotes(NotesChangedEvent notes)
    {
        if (notes.Add != null)
            NoteAdded?.Invoke(notes.Add.Value);

        if (notes.Update != null)
            NoteChanged?.Invoke(notes.Update.Value);

        if (notes.Remove != null)
            NoteRemoved?.Invoke(notes.Remove.Value);

        return ValueTask.CompletedTask;
    }

    // ValueTask is kind of a hint that this might be a different, unknown thread.
    // And it also lets me use a clean and convenient switch.
    private ValueTask Enqueue(BaseEvent baseEvent)
    {
        _eventQueue.Enqueue(baseEvent);
        return ValueTask.CompletedTask;
    }
    private void OnNullLinkConnected() => _ = Resubscribe();

    private void OnResubscribeTimer() => _ = ResubscribeAndRescheduleAsync();

    private async Task ResubscribeAndRescheduleAsync()
    {
        var success = await Resubscribe();

        TimeSpan next;
        if (success)
        {
            _resubscribeFailures = 0;
            next = _grainDelay;
        }
        else
        {
            var index = Math.Min(_resubscribeFailures++, _resubscribeBackoff.Length - 1);
            next = _resubscribeBackoff[index];
        }

        try
        {
            _resubscribeTimer?.Change(next, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async ValueTask<bool> Resubscribe()
    {
        if (!_actors.Enabled)
            return true;

        try
        {
            if (!_actors.TryGetServerGrain(out var serverGrain))
            {
                _sawmill.Log(LogLevel.Warning, "Failed to get server grain for resubscription.");
                return false;
            }
            if (!_actors.TryCreateObjectReference<IEventBusObserver>(this, out var eventBusObserver) || eventBusObserver is null)
            {
                _sawmill.Log(LogLevel.Warning, "Failed to create event bus observer reference.");
                return false;
            }

            await serverGrain.ResubscribeEventBus(eventBusObserver);
            return true;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to resubscribe to the NullLink event bus: {ex}");
            return false;
        }
    }
}

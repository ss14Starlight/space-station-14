using System.Security.Cryptography;
using Content.Shared._Starlight.Silicons;
using Content.Shared._Starlight.Computers.RemoteControl;

namespace Content.Server._Starlight.Silicons;

public sealed partial class RemoteControlBrainSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;

    private readonly HashSet<int> _usedChannels = new();
    private readonly Dictionary<int, EntityUid> _brainsByChannel = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RemoteControlBrainComponent, MapInitEvent>(OnMapInit);
    }

    public bool TryGetBrain(int channel, out EntityUid brain)
    {
        if (RemoteControlChannel.IsValid(channel)
            && _brainsByChannel.TryGetValue(channel, out brain)
            && !TerminatingOrDeleted(brain))
            return true;

        brain = default;
        return false;
    }
    [SubscribeLocalEvent]
    private void OnStartup(Entity<RemoteControlBrainComponent> entity, ref ComponentStartup args)
    {
        if (_mapSystem.IsInitialized(Transform(entity).MapID))
            RegisterBrain(entity);
    }

    private void OnMapInit(Entity<RemoteControlBrainComponent> entity, ref MapInitEvent args) => RegisterBrain(entity);

    private void RegisterBrain(Entity<RemoteControlBrainComponent> entity)
    {
        var channel = entity.Comp.Channel;
        if (RemoteControlChannel.IsValid(channel)
            && _brainsByChannel.TryGetValue(channel, out var registeredBrain)
            && registeredBrain == entity.Owner)
            return;

        if (!RemoteControlChannel.IsValid(channel) || !_usedChannels.Add(channel))
        {
            channel = AllocateChannel();
            entity.Comp.Channel = channel;
            Dirty(entity);
        }

        _brainsByChannel.Add(channel, entity.Owner);
        RaiseLocalEvent(new RemoteControlBrainChannelRegisteredEvent(entity.Owner, channel));
    }
    [SubscribeLocalEvent]
    private void OnShutdown(Entity<RemoteControlBrainComponent> entity, ref ComponentShutdown args)
    {
        var channel = entity.Comp.Channel;
        if (!_brainsByChannel.TryGetValue(channel, out var brain) || brain != entity.Owner)
            return;

        _brainsByChannel.Remove(channel);
        _usedChannels.Remove(channel);
        RaiseLocalEvent(new RemoteControlBrainChannelUnregisteredEvent(entity.Owner, channel));
    }

    private int AllocateChannel()
    {
        if (_usedChannels.Count >= RemoteControlChannel.ChannelCount)
            throw new InvalidOperationException("No remote control channels remain available.");

        var start = RandomNumberGenerator.GetInt32(RemoteControlChannel.ChannelCount);
        for (var offset = 0; offset < RemoteControlChannel.ChannelCount; offset++)
        {
            var channel = (start + offset) % RemoteControlChannel.ChannelCount;
            if (_usedChannels.Add(channel))
                return channel;
        }

        throw new InvalidOperationException("No remote control channels remain available.");
    }
}

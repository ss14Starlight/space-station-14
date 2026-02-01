using System.Linq;
using System.Reflection;
using Content.Shared._Starlight.Radio;
using Content.Shared.Administration;
using Content.Shared.Implants.Components;
using Content.Shared.Radio.Components;
using Robust.Shared.Toolshed;

namespace Content.Server.Administration.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class RadioCommand : ToolshedCommand
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    
    private static Type[] _parsers = [typeof(ISupportsCustomChannelsTypeParser)];
    public override Type[] TypeParameterParsers => _parsers;
    
    [CommandImplementation("addcustom")]
    public EntityUid Create<T>([PipedArgument] EntityUid uid, string id,
        string name, char keycode, int frequency, string hex, bool longRange, bool ensure = false) where T : ISupportsCustomChannels, IComponent, new()
    {
        T? comp;
        if (ensure) comp = EnsureComp<T>(uid);
        else if (!TryComp<T>(uid, out comp)) return uid;
        if (comp.CustomChannels.Any(x => x.Id == id)) return uid;
        comp.CustomChannels.Add(new CustomRadioChannelData
        {
            Id = id,
            Name = name,
            Color = Color.FromHex(hex),
            Frequency = frequency,
            Keycode = keycode,
            LongRange = longRange,
        });
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("addcustom")]
    public IEnumerable<EntityUid> Create<T>([PipedArgument] IEnumerable<EntityUid> uid, string id,
        string name, char keycode, int frequency, string hex, bool longRange, bool ensure = false)
        where T : ISupportsCustomChannels, IComponent, new() => uid.Select(x =>
        Create<T>(x, id, name, keycode, frequency, hex, longRange, ensure));

    [CommandImplementation("remcustom")]
    public EntityUid Delete<T>([PipedArgument] EntityUid uid, string id) where T : ISupportsCustomChannels, IComponent
    {
        if (!TryComp<T>(uid, out var comp)) return uid;
        comp.CustomChannels.RemoveWhere(c => c.Id == id);
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("remcustom")]
    public IEnumerable<EntityUid> Delete<T>([PipedArgument] IEnumerable<EntityUid> uid, Type componentType, string id)
        where T : ISupportsCustomChannels, IComponent =>
        uid.Select(x => Delete<T>(x, id));
}
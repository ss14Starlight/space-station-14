using System.Collections;
using System.IO;
using System.Linq;
using Content.Server._Starlight.Toolshed;
using Content.Server.Administration;
using Content.Shared._Starlight.Commands;
using Content.Shared._Starlight.ViewVariables;
using Content.Shared.Administration;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.VarEdit)]
public sealed partial class VvCommand : ToolshedCommand
{
    [Dependency] private IViewVariablesManager _vvm = null!;
    [Dependency] private IEntityNetworkManager _net = null!;
    [Dependency] private ISerializationManager _serMan = null!;

    [CommandImplementation("open")]
    public EntityUid Vv(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _net.SendSystemNetworkMessage(new OpenViewVariablesEvent($"{EntityManager.GetNetEntity(uid).Id}"),
            ctx.Session!.Channel);
        return uid;
    }

    [CommandImplementation("open")]
    public void Vv(IInvocationContext ctx, [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        _net.SendSystemNetworkMessage(new OpenViewVariablesEvent($"/{path}"),
            ctx.Session!.Channel);
    }

    [CommandImplementation("write")]
    public EntityUid Write([PipedArgument] EntityUid uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path, string value)
    {
        if (path.StartsWith('/')) path = path[1..];
        _vvm.WritePath($"/entity/{uid}/{path}", value);
        return uid;
    }

    [CommandImplementation("write")]
    public void Write([CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path, string value)
    {
        if (path.StartsWith('/')) path = path[1..];
        _vvm.WritePath(path, value);
    }

    [CommandImplementation("owrite")]
    public EntityUid ObjectWrite(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path, VarRef<object?> value)
    {
        if (path.StartsWith('/')) path = path[1..];
        WriteObject(ctx, $"/entity/{uid}/{path}", value);
        return uid;
    }

    [CommandImplementation("owrite")]
    public void ObjectWrite(IInvocationContext ctx,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path, VarRef<object?> value)
    {
        if (path.StartsWith('/')) path = path[1..];
        WriteObject(ctx, path, value);
    }

    private void WriteObject(IInvocationContext ctx, string path, VarRef<object?> value)
    {
        var resPath = _vvm.ResolvePath(path);
        if (resPath is null)
        {
            ctx.WriteLine("Could not find path.");
            return;
        }

        var val = ctx.ReadVar(value.VarName);
        var targetType = resPath.Get()?.GetType();

        if (targetType is null)
        {
            ctx.WriteLine("Path leads to a null type.");
            return;
        }

        if (targetType != val?.GetType())
            if (!targetType.IsNullable() && val is null)
            {
                ctx.WriteLine("Type is not nullable.");
                return;
            }

        resPath.Set(Convert.ChangeType(val, targetType));
    }

    [CommandImplementation("read")]
    public EntityUid Read(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
    {
        var val = GetViewVariable(uid, path);
        ctx.WriteLine(val ?? "null");
        return uid;
    }

    [CommandImplementation("read")]
    public void Read(IInvocationContext ctx, [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        var val = _vvm.ReadPathSerialized(path);
        ctx.WriteLine(val ?? "null");
    }

    [CommandImplementation("rsave")]
    public string RSave([PipedArgument] EntityUid uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
    {
        var val = GetViewVariable(uid, path);
        return val ?? "null";
    }

    [CommandImplementation("rsave")]
    public string RSave([CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        var val = _vvm.ReadPathSerialized(path);
        return val ?? "null";
    }

    [CommandImplementation("rsaveraw")]
    public object? RSaveRaw([PipedArgument] EntityUid uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        var val = _vvm.ReadPath($"/entity/{uid}/{path}");
        return val;
    }

    [CommandImplementation("rsaveraw")]
    public object? RSaveRaw([CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        return _vvm.ReadPath(path);
    }

    [CommandImplementation("append")]
    public EntityUid Append(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path, string value)
    {
        if (path.StartsWith('/')) path = path[1..];
        AppendPath(ctx, $"/entity/{uid}/{path}", value);
        return uid;
    }

    [CommandImplementation("append")]
    public void Append(IInvocationContext ctx,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path, string value)
    {
        if (path.StartsWith('/')) path = path[1..];
        AppendPath(ctx, path, value);
    }

    [CommandImplementation("rmat")]
    public EntityUid RemoveAt(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path,
        [CommandArgument(typeof(ViewVariablesIndexCompletionParser))] int index)
    {
        if (path.StartsWith('/')) path = path[1..];
        RemoveAtPath(ctx, $"/entity/{uid}/{path}", index);
        return uid;
    }

    [CommandImplementation("rmat")]
    public void RemoveAt(IInvocationContext ctx,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path,
        [CommandArgument(typeof(ViewVariablesIndexCompletionParser))] int index)
    {
        if (path.StartsWith('/')) path = path[1..];
        RemoveAtPath(ctx, path, index);
    }

    private void AppendPath(IInvocationContext ctx, string path, string value)
    {
        var resolved = _vvm.ResolvePath(path);
        if (resolved?.Get() is not { } collection)
        {
            CommandMarkup.Error(ctx, "Could not find path, or the value at the path is null.");
            return;
        }

        var type = collection.GetType();

        if (collection is IDictionary dict && GetGenericInterface(type, typeof(IDictionary<,>)) is { } dictInterface)
        {
            if (dict.IsReadOnly)
            {
                CommandMarkup.Error(ctx, $"{TypeName(type)} is read-only.");
                return;
            }

            var entriesType = typeof(Dictionary<,>).MakeGenericType(dictInterface.GenericTypeArguments);
            if (DeserializeValue(entriesType, value) is not (IDictionary entries, true))
            {
                CommandMarkup.Error(ctx, $"Failed to deserialize value to {TypeName(entriesType)}.");
                return;
            }

            foreach (DictionaryEntry entry in entries)
            {
                if (dict.Contains(entry.Key))
                {
                    CommandMarkup.Warn(ctx, $"Key {entry.Key} already exists, skipping.");
                    continue;
                }

                dict.Add(entry.Key, entry.Value);
            }

            return;
        }

        if (GetGenericInterface(type, typeof(ICollection<>)) is not { } collectionInterface)
        {
            CommandMarkup.Error(ctx, $"{TypeName(type)} is not a collection.");
            return;
        }

        var elementType = collectionInterface.GenericTypeArguments[0];
        if (!TryDeserializeElements(elementType, value, out var elements))
        {
            CommandMarkup.Error(ctx,
                $"Failed to deserialize value to {TypeName(elementType)} or a sequence of {TypeName(elementType)}.");
            return;
        }

        // Arrays can't be resized, so build a new one and write it back.
        if (collection is Array array)
        {
            var newArray = Array.CreateInstance(elementType, array.Length + elements.Count);
            array.CopyTo(newArray, 0);
            for (var i = 0; i < elements.Count; i++)
                newArray.SetValue(elements[i], array.Length + i);
            resolved.Set(newArray);
            return;
        }

        if ((bool) collectionInterface.GetProperty(nameof(ICollection<>.IsReadOnly))!.GetValue(collection)!)
        {
            CommandMarkup.Error(ctx, $"{TypeName(type)} is read-only.");
            return;
        }

        var add = collectionInterface.GetMethod(nameof(ICollection<>.Add))!;
        foreach (var element in elements)
            add.Invoke(collection, [element]);
    }

    private void RemoveAtPath(IInvocationContext ctx, string path, int index)
    {
        var resolved = _vvm.ResolvePath(path);
        if (resolved?.Get() is not IEnumerable enumerable || enumerable is string)
        {
            CommandMarkup.Error(ctx, "Could not find path, or the value at the path is not a collection.");
            return;
        }

        var type = enumerable.GetType();
        var items = enumerable.Cast<object?>().ToList();
        if (index < 0 || index >= items.Count)
        {
            CommandMarkup.Error(ctx, $"Index {index} is out of range for a collection of {items.Count} elements.");
            return;
        }

        switch (enumerable)
        {
            case Array array:
                {
                    var newArray = Array.CreateInstance(type.GetElementType()!, array.Length - 1);
                    Array.Copy(array, 0, newArray, 0, index);
                    Array.Copy(array, index + 1, newArray, index, array.Length - index - 1);
                    resolved.Set(newArray);
                    return;
                }
            case IList { IsReadOnly: false, IsFixedSize: false } list:
                list.RemoveAt(index);
                return;
            case IDictionary { IsReadOnly: false } dict:
                dict.Remove(dict.Keys.Cast<object>().ElementAt(index));
                return;
        }

        if (GetGenericInterface(type, typeof(ICollection<>)) is not { } collectionInterface
            || (bool)collectionInterface.GetProperty(nameof(ICollection<>.IsReadOnly))!.GetValue(enumerable)!)
        {
            CommandMarkup.Error(ctx, $"{TypeName(type)} does not support removing elements.");
            return;
        }

        collectionInterface.GetMethod(nameof(ICollection<>.Remove))!.Invoke(enumerable, [items[index]]);
    }

    private bool TryDeserializeElements(Type elementType, string value, out List<object?> elements)
    {
        if (DeserializeValue(elementType, value) is (var element, true))
        {
            elements = [element];
            return true;
        }

        if (DeserializeValue(typeof(List<>).MakeGenericType(elementType), value) is (IEnumerable sequence, true))
        {
            elements = [.. sequence.Cast<object?>()];
            return true;
        }

        elements = [];
        return false;
    }

    private static Type? GetGenericInterface(Type type, Type genericInterface) =>
        type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface);

    private static string TypeName(Type type) => PrettyPrint.PrintUserFacingTypeShort(type, 2);

    [CommandImplementation("open")]
    public IEnumerable<EntityUid> Vv(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => Vv(ctx, x));

    [CommandImplementation("write")]
    public IEnumerable<EntityUid> Write([PipedArgument] IEnumerable<EntityUid> uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path, string value)
        => uid.Select(x => Write(x, path, value));

    [CommandImplementation("read")]
    public IEnumerable<EntityUid> Read(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
        => uid.Select(x => Read(ctx, x, path));

    [CommandImplementation("rsave")]
    public IEnumerable<string> RSave([PipedArgument] IEnumerable<EntityUid> uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
        => uid.Select(x => RSave(x, path));

    [CommandImplementation("rsaveraw")]
    public IEnumerable<object?> RSaveRaw([PipedArgument] IEnumerable<EntityUid> uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
        => uid.Select(x => RSaveRaw(x, path));

    [CommandImplementation("append")]
    public IEnumerable<EntityUid> Append(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path, string value)
        => uid.Select(x => Append(ctx, x, path, value));

    [CommandImplementation("rmat")]
    public IEnumerable<EntityUid> RemoveAt(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path,
        [CommandArgument(typeof(ViewVariablesIndexCompletionParser))] int index)
        => uid.Select(x => RemoveAt(ctx, x, path, index));

    private string? GetViewVariable(EntityUid uid,
        [CommandArgument(typeof(ViewVariablesPathCompletionParser))] string path)
    {
        if (path.StartsWith('/')) path = path[1..];
        var val = _vvm.ReadPathSerialized($"/entity/{uid}/{path}");
        return val;
    }

    // Reimplementation of method from view variables manager because private :/
    private (object?, bool) DeserializeValue(Type type, string value)
    {
        if (_vvm.ResolvePath(value)?.Get() is {} resolved && resolved.GetType().IsAssignableTo(type))
            return (resolved, true);

        try
        {
            using TextReader stream = new StringReader(value);
            var yamlStream = new YamlStream();
            yamlStream.Load(stream);
            var document = yamlStream.Documents[0];
            var rootNode = document.RootNode;
            return (_serMan.Read(type, rootNode.ToDataNode()), true);
        }
        catch (Exception)
        {
            return (null, false);
        }
    }
}

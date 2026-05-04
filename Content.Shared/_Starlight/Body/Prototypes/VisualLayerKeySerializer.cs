// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared._Starlight.Body.Prototypes;

[TypeSerializer]
public sealed class VisualLayerKeySerializer : ITypeSerializer<VisualLayerKey, ValueDataNode>, ITypeCopyCreator<VisualLayerKey>
{
    public ValidationNode Validate(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        if (!VisualLayerKey.TryParse(node.Value, out var key))
            return new ErrorNode(node, $"Invalid VisualLayerKey '{node.Value}'. Expected 'LayerId', 'LayerId{VisualLayerKey.Separator}Index', 'LayerId{VisualLayerKey.DisplacementSuffix}', or 'LayerId{VisualLayerKey.Separator}Index{VisualLayerKey.DisplacementSuffix}'.");

        var protoMan = dependencies.Resolve<IPrototypeManager>();
        return !protoMan.HasIndex(key.Layer)
            ? new ErrorNode(node, $"VisualLayerPrototype '{key.Layer.Id}' was not found.")
            : new ValidatedValueNode(node);
    }

    public VisualLayerKey Read(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<VisualLayerKey>? instanceProvider = null)
        => VisualLayerKey.Parse(node.Value);

    public DataNode Write(
        ISerializationManager serializationManager,
        VisualLayerKey value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
        => new ValueDataNode(value.ToString());

    public VisualLayerKey CreateCopy(
        ISerializationManager serializationManager,
        VisualLayerKey source,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null)
        => source;
}

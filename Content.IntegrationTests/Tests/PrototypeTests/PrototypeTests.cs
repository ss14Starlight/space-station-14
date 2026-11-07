#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.PrototypeTests;

public sealed class PrototypeTests : GameTest
{
    #region Starlight
    /// <summary>
    /// Validates that all server and client prototypes can be serialized as valid yaml.
    /// </summary>
    [Test]
    public async Task TestAllServerAndClientPrototypesAreSerializable()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var serverContext = new PrototypeSaveTest.TestEntityUidContext(
            server.ResolveDependency<ISerializationManager>());
        var clientContext = new PrototypeSaveTest.TestEntityUidContext(
            client.ResolveDependency<ISerializationManager>());
        await SaveThenValidatePrototype(server, "server", serverContext);
        await SaveThenValidatePrototype(client, "client", clientContext);
    }
    #endregion

    public async Task SaveThenValidatePrototype(RobustIntegrationTest.IntegrationInstance instance, string instanceId,
        PrototypeSaveTest.TestEntityUidContext ctx)
    {
        var protoMan = instance.ResolveDependency<IPrototypeManager>();
        Dictionary<Type, Dictionary<string, HashSet<ErrorNode>>> errors = default!;
        await instance.WaitPost(() => errors = protoMan.ValidateAllPrototypesSerializable(ctx));

        if (errors.Count == 0)
            return;

        Assert.Multiple(() =>
        {
            foreach (var (kind, ids) in errors)
            {
                foreach (var (id, nodes) in ids)
                {
                    var msg = $"Error when validating {instanceId} prototype ({kind.Name}, {id}). Errors: \n";
                    foreach (var errorNode in nodes)
                    {
                        msg += $" - {errorNode.ErrorReason}\n";
                    }
                    Assert.Fail(msg);
                }
            }
        });
    }

    #region Starlight
    /// <summary>
    /// Checks that server and client prototypes remain unchanged after saving and loading.
    /// </summary>
    [Test]
    public async Task ServerAndClientPrototypeSaveLoadSaveTest()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var serverContext = new PrototypeSaveTest.TestEntityUidContext(
            server.ResolveDependency<ISerializationManager>());
        var clientContext = new PrototypeSaveTest.TestEntityUidContext(
            client.ResolveDependency<ISerializationManager>());
        await SaveLoadSavePrototype(server, serverContext);
        await SaveLoadSavePrototype(client, clientContext);
    }
    #endregion

    private async Task SaveLoadSavePrototype(
        RobustIntegrationTest.IntegrationInstance instance,
        PrototypeSaveTest.TestEntityUidContext ctx)
    {
        var protoMan = instance.ResolveDependency<IPrototypeManager>();
        var seriMan = instance.ResolveDependency<ISerializationManager>();
        await instance.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var kind in protoMan.EnumeratePrototypeKinds())
                {
                    foreach (var proto in protoMan.EnumeratePrototypes(kind))
                    {
                        var noException = TrySaveLoadSavePrototype(
                            seriMan,
                            protoMan,
                            kind,
                            proto,
                            ctx);

                        // This will probably throw an exception for each prototype of this kind.
                        // We want to avoid having tests crash because they run out of time.
                        if (!noException)
                            break;
                    }
                }
            });
        });
    }

    /// <returns>False if an exception was caught</returns>
    private bool TrySaveLoadSavePrototype(
        ISerializationManager seriMan,
        IPrototypeManager protoMan,
        Type kind,
        IPrototype proto,
        PrototypeSaveTest.TestEntityUidContext ctx)
    {
        DataNode first;
        DataNode second;

        try
        {
            first = seriMan.WriteValue(kind, proto, alwaysWrite: true, context:ctx);
        }
        catch (Exception e)
        {
            protoMan.TryGetMapping(kind, proto.ID, out var mapping);
            Assert.Fail($"Caught exception while writing {kind.Name} prototype {proto.ID}. Exception:\n{e}");
            return false;
        }

        object? obj;
        try
        {
            obj = seriMan.Read(kind, first, context:ctx);
        }
        catch (Exception e)
        {
            protoMan.TryGetMapping(kind, proto.ID, out var mapping);
            Assert.Fail($"Caught exception while re-reading {kind.Name} prototype {proto.ID}." +
                        $"\nException:\n{e}" +
                        $"\n\nOriginal yaml:\n{mapping}" +
                        $"\n\nWritten yaml:\n{first}");
            return false;
        }

        Assert.That(obj?.GetType(), Is.EqualTo(proto.GetType()));
        var deserialized = (IPrototype) obj!;

        try
        {
            second = seriMan.WriteValue(kind, deserialized, alwaysWrite: true, context:ctx);
        }
        catch (Exception e)
        {
            protoMan.TryGetMapping(kind, proto.ID, out var mapping);
            Assert.Fail($"Caught exception while re-writing {kind.Name} prototype {proto.ID}." +
                        $"\nException:\n{e}" +
                        $"\n\nOriginal yaml:\n{mapping}" +
                        $"\n\nWritten yaml:\n{first}");
            return false;
        }

        var diff = first.Except(second);
        if (diff == null || diff.IsEmpty)
            return true;

        protoMan.TryGetMapping(kind, proto.ID, out var orig);
        Assert.Fail($"Re-written {kind.Name} prototype {proto.ID} differs." +
                    $"\nYaml diff:\n{diff}" +
                    $"\n\nOriginal yaml:\n{orig}" +
                    $"\n\nWritten yaml:\n{first}" +
                    $"\n\nRe-written Yaml:\n{second}");
        return true;
    }
}

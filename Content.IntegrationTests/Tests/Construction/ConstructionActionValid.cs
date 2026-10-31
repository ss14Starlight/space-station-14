using System.Text;
using Content.IntegrationTests.Fixtures;
using Content.Server.Construction.Completions;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Construction
{
    [TestFixture]
    public sealed class ConstructionActionValid : GameTest
    {
        private bool IsValid(IGraphAction action, IPrototypeManager protoMan, out string prototype)
        {
            switch (action)
            {
                case SpawnPrototype spawn:
                    prototype = spawn.Prototype;
                    return protoMan.TryIndex<EntityPrototype>(spawn.Prototype, out _);
                case SpawnPrototypeAtContainer spawn:
                    prototype = spawn.Prototype;
                    return protoMan.TryIndex<EntityPrototype>(spawn.Prototype, out _);
                case ConditionalAction conditional:
                    var valid = IsValid(conditional.Action, protoMan, out var protoA) & IsValid(conditional.Else, protoMan, out var protoB);

                    if (!string.IsNullOrEmpty(protoA) && string.IsNullOrEmpty(protoB))
                    {
                        prototype = protoA;
                    }

                    else if (string.IsNullOrEmpty(protoA) && !string.IsNullOrEmpty(protoB))
                    {
                        prototype = protoB;
                    }

                    else
                    {
                        prototype = $"{protoA}, {protoB}";
                    }

                    return valid;
                default:
                    prototype = string.Empty;
                    return true;
            }
        }

        #region Starlight
        [Test]
        public async Task ConstructionGraphActionsAndEdgesValid()
        {
            var pair = Pair;
            var server = pair.Server;

            var protoMan = server.ResolveDependency<IPrototypeManager>();

            var valid = true;
            var message = new StringBuilder();

            await server.WaitPost(() =>
            {
                foreach (var graph in protoMan.EnumeratePrototypes<ConstructionGraphPrototype>())
                {
                    foreach (var node in graph.Nodes.Values)
                    {
                        foreach (var action in node.Actions)
                        {
                            if (IsValid(action, protoMan, out var prototype)) continue;

                            valid = false;
                            message.Append($"Invalid entity prototype \"{prototype}\" on graph action in node \"{node.Name}\" of graph \"{graph.ID}\"\n");
                        }

                        foreach (var edge in node.Edges)
                        {
                            if (!graph.Nodes.ContainsKey(edge.Target))
                            {
                                valid = false;
                                message.Append(
                                    $"Invalid target \"{edge.Target}\" in edge on node \"{node.Name}\" of graph \"{graph.ID}\"\n");
                            }

                            foreach (var action in edge.Completed)
                            {
                                if (IsValid(action, protoMan, out var prototype)) continue;

                                valid = false;
                                message.Append($"Invalid entity prototype \"{prototype}\" on graph action in edge \"{edge.Target}\" of node \"{node.Name}\" of graph \"{graph.ID}\"\n");
                            }
                        }
                    }
                }
            });

            Assert.That(valid, Is.True,
                $"One or more construction graph actions or edges are invalid!\n{message}");
        }
        #endregion
    }
}

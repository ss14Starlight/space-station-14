/*
 * Commented out for the same reason that FixtureCommand.cd is commented out.
 * Want to make PR, don't want to lose progress, feel like this is the easiest way to do it.
 */
// using System.Linq;
// using System.Numerics;
// using Content.Server.Administration;
// using Content.Shared.Administration;
// using Robust.Shared.Toolshed;
//
// namespace Content.Server._Starlight.Administration.Systems.Commands;
//
// [AdminCommand(AdminFlags.Fun)]
// [ToolshedCommand]
// public sealed class Vector2DataConstructorCommand : ToolshedCommand
// {
//     [CommandImplementation("new")]
//     public EntityUid New([PipedArgument] EntityUid uid)
//     {
//         EnsureComp<Vector2DataConstructorComponent>(uid);
//         return uid;
//     }
//
//     [CommandImplementation("new")]
//     public IEnumerable<EntityUid> New([PipedArgument] IEnumerable<EntityUid> uid) => uid.Select(New);
//
//     [CommandImplementation("add")]
//     public EntityUid Add([PipedArgument] EntityUid uid, float x, float y)
//     {
//         var comp = Comp<Vector2DataConstructorComponent>(uid);
//         comp.Vertices.Add(new Vector2(x, y));
//         return uid;
//     }
//
//     [CommandImplementation("add")]
//     public IEnumerable<EntityUid> Add(IEnumerable<EntityUid> uid, float x, float y) => uid.Select(u => Add(u, x, y));
//
//     [CommandImplementation("clean")]
//     public EntityUid Clean([PipedArgument] EntityUid uid)
//     {
//         RemComp<Vector2DataConstructorComponent>(uid);
//         return uid;
//     }
//
//     [CommandImplementation("clean")]
//     public IEnumerable<EntityUid> Clean(IEnumerable<EntityUid> uid) => uid.Select(Clean);
// }
//
// [RegisterComponent]
// public sealed partial class Vector2DataConstructorComponent : Component
// {
//     [DataField] public List<Vector2> Vertices = [];
// }

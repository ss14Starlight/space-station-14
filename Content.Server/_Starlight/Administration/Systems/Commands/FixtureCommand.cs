/*
 * Leaving this in here so that I can come back to it later.
 * Need to figure out a way to properly create physics shapes. Will require an RT pull request.
 * Currently, I have been able to make it create the AABB but there's an issue with shape type being unknown
 * which is causing issues on specifically the client, despite the fact that AABB's are always unknown shape type???
 * IDK it's really stupid and weird. But either way I don't want to lose progress on this so that's why it's here
 * commented out, so that I can finish this fucking pull request.
 */
// using System.Linq;
// using System.Numerics;
// using Content.Server.Administration;
// using Content.Shared.Administration;
// using Robust.Server.GameObjects;
// using Robust.Shared.Physics;
// using Robust.Shared.Physics.Collision.Shapes;
// using Robust.Shared.Physics.Components;
// using Robust.Shared.Physics.Systems;
// using Robust.Shared.Toolshed;
//
// namespace Content.Server._Starlight.Administration.Systems.Commands;
//
// [ToolshedCommand, AdminCommand(AdminFlags.Fun)]
// public sealed class FixtureCommand : ToolshedCommand
// {
//     private FixtureSystem? _fixture;
//     private PhysicsSystem? _phys;
//
//     [CommandImplementation("createpoly")]
//     public EntityUid CreateFixture([PipedArgument] EntityUid uid, string id, float density,
//         bool hard,
//         int collisionLayer, int collisionMask, float friction, float restitution)
//     {
//         _fixture ??= GetSys<FixtureSystem>();
//         _phys ??= GetSys<PhysicsSystem>();
//         if (!TryComp<Vector2DataConstructorComponent>(uid, out var vertices)) return uid;
//         var phys = EnsureComp<PhysicsComponent>(uid);
//         var fixtures = EnsureComp<FixturesComponent>(uid);
//         var xform = EnsureComp<TransformComponent>(uid);
//         var shape = new PolygonShape(vertices.Vertices.ToArray());
//         if (!_fixture.TryCreateFixture(uid, shape, id, density, hard, collisionLayer, collisionMask,
//                 friction, restitution, true, fixtures, phys, xform))
//             return uid;
//         var result = _fixture.GetFixtureOrNull(uid, id, fixtures);
//         if (result is null) return uid;
//         // _phys.SetVertices(uid, id, result, shape, vertices.Vertices.ToArray(), fixtures, phys, xform);
//         RemComp<Vector2DataConstructorComponent>(uid);
//         return uid;
//     }
//
//     [CommandImplementation("createcircle")]
//     public EntityUid CreateFixture([PipedArgument] EntityUid uid, string id, float radius, float x, float y,
//         float density, bool hard,
//         int collisionLayer, int collisionMask, float friction, float restitution)
//     {
//         _fixture ??= GetSys<FixtureSystem>();
//         _phys ??= GetSys<PhysicsSystem>();
//         var phys = EnsureComp<PhysicsComponent>(uid);
//         var fixtures = EnsureComp<FixturesComponent>(uid);
//         var xform = EnsureComp<TransformComponent>(uid);
//         var shape = new PhysShapeCircle();
//         if (!_fixture.TryCreateFixture(uid, shape, id, density, hard, collisionLayer, collisionMask,
//                 friction, restitution, true, fixtures, phys, xform))
//             return uid;
//         var result = _fixture.GetFixtureOrNull(uid, id, fixtures);
//         if (result is null) return uid;
//         _phys.SetPositionRadius(uid, id, result, shape, new Vector2(x, y), radius, fixtures, phys, xform);
//         return uid;
//     }
//
//     [CommandImplementation("createaabb")]
//     public EntityUid CreateFixture([PipedArgument] EntityUid uid, string id, float x1, float y1, float x2, float y2,
//         float density, bool hard,
//         int collisionLayer, int collisionMask, float friction, float restitution)
//     {
//         _fixture ??= GetSys<FixtureSystem>();
//         _phys ??= GetSys<PhysicsSystem>();
//         var phys = EnsureComp<PhysicsComponent>(uid);
//         var fixtures = EnsureComp<FixturesComponent>(uid);
//         var xform = EnsureComp<TransformComponent>(uid);
//         var shape = new PhysShapeAabb(new Box2(x1, y1, x2, y2));
//         _fixture.TryCreateFixture(uid, shape, id, density, hard, collisionLayer, collisionMask,
//             friction, restitution, true, fixtures, phys, xform);
//         return uid;
//     }
//
//     [CommandImplementation("createpoly")]
//     public IEnumerable<EntityUid> CreateFixture([PipedArgument] IEnumerable<EntityUid> uid, string id,
//         float density, bool hard,
//         int collisionLayer, int collisionMask, float friction, float restitution) =>
//         uid.Select(x =>
//             CreateFixture(x, id, density, hard, collisionLayer, collisionMask, friction, restitution));
//
//     [CommandImplementation("createcircle")]
//     public IEnumerable<EntityUid> CreateFixture([PipedArgument] IEnumerable<EntityUid> uid, string id, float radius,
//         float xpos, float ypos, float density, bool hard,
//         int collisionLayer, int collisionMask, float friction, float restitution) =>
//         uid.Select(x =>
//             CreateFixture(x, id, radius, xpos, ypos, density, hard, collisionLayer, collisionMask, friction,
//                 restitution));
//
//     [CommandImplementation("delete")]
//     public EntityUid DeleteFixture([PipedArgument] EntityUid uid, string id)
//     {
//         _fixture ??= GetSys<FixtureSystem>();
//         _fixture.DestroyFixture(uid, id);
//         return uid;
//     }
//
//     [CommandImplementation("delete")]
//     public IEnumerable<EntityUid> DeleteFixture([PipedArgument] IEnumerable<EntityUid> uid, string id) =>
//         uid.Select(x => DeleteFixture(x, id));
// }

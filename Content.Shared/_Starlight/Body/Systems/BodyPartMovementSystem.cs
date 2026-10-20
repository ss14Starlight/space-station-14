// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Movement.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Physics;

namespace Content.Shared._Starlight.Body.Systems;

/// <summary>
/// Movement speed of an <see cref="SLBodyComponent"/> body comes from its attached legs
/// (<see cref="MovementBodyPartComponent"/>). Losing every leg knocks the body down.
/// </summary>
public sealed partial class BodyPartMovementSystem : EntitySystem
{
    [Dependency] private SLBodySystem _body = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    /// <summary>
    /// Density used when the body has no "fix1" fixture, matching the legacy body.
    /// </summary>
    private const float DefaultDensity = 185f;

    [SubscribeLocalEvent]
    private void OnLegEntered(Entity<MovementBodyPartComponent> ent, ref BodyPartEnteredBodyEvent args)
        => UpdateMovementSpeed(args.Body);

    [SubscribeLocalEvent]
    private void OnLegLeft(Entity<MovementBodyPartComponent> ent, ref BodyPartLeftBodyEvent args)
    {
        var legs = UpdateMovementSpeed(args.Body);
        if (legs != 0 || args.Body.Comp.RequiredLegs <= 0)
            return;

        if (!TryComp<StandingStateComponent>(args.Body, out var standing)
            || !standing.Standing
            || !_standing.Down(args.Body, standingState: standing))
            return;

        var ev = new DropHandItemsEvent();
        RaiseLocalEvent(args.Body, ref ev);
    }

    /// <summary>
    /// Recomputes the body's base speed from its legs. Returns the number of attached legs.
    /// </summary>
    public int UpdateMovementSpeed(Entity<SLBodyComponent> body)
    {
        if (body.Comp.RequiredLegs <= 0 || !TryComp<MovementSpeedModifierComponent>(body, out var movement))
            return 0;

        var legs = 0;
        var walkSpeed = 0f;
        var sprintSpeed = 0f;
        var acceleration = 0f;
        var maxDensity = 0f;
        var minSpeedMod = 0f;
        var maxSpeedMod = 0f;
        foreach (var leg in _body.GetBodyPartsWithComp<MovementBodyPartComponent>(body))
        {
            if (_body.IsInternal((leg.Owner, leg.Comp1)))
                continue;

            legs++;
            walkSpeed += leg.Comp2.WalkSpeed;
            sprintSpeed += leg.Comp2.SprintSpeed;
            acceleration += leg.Comp2.Acceleration;
            maxDensity += leg.Comp2.MaxDensity;
            minSpeedMod += leg.Comp2.MinSpeedMod;
            maxSpeedMod += leg.Comp2.MaxSpeedMod;
        }

        var density = TryComp<FixturesComponent>(body, out var fixtures)
            && fixtures.Fixtures.TryGetValue("fix1", out var fixture)
            ? fixture.Density
            : DefaultDensity;

        // Legs carry a limited density; a heavier body moves proportionally slower.
        var speedFactor = density > maxDensity && maxDensity > 0f ? maxDensity / density : 1f;
        var required = body.Comp.RequiredLegs;
        _movement.ChangeBaseSpeed(body,
            walkSpeed * speedFactor / required,
            sprintSpeed * speedFactor / required,
            acceleration * speedFactor / required,
            movement,
            minSpeedMod / required,
            maxSpeedMod / required);
        return legs;
    }
}

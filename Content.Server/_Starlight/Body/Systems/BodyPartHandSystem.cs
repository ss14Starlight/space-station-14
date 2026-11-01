// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Server.Hands.Systems;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Events;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared.Hands.Components;

namespace Content.Server._Starlight.Body.Systems;

/// <summary>
/// Adds a hand to the body for every attached <see cref="BodyPartHandComponent"/> part and removes it (dropping what
/// it held) when the part leaves the body. Server-only, the hands state is networked.
/// </summary>
public sealed partial class BodyPartHandSystem : EntitySystem
{
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SLBodySystem _body = default!;

    [SubscribeLocalEvent]
    private void OnEnteredBody(Entity<BodyPartHandComponent> ent, ref BodyPartEnteredBodyEvent args)
    {
        if (!TryComp<HandsComponent>(args.Body, out var hands) || _body.IsInternal(args.Part))
            return;

        var location = ent.Comp.Location ?? _body.GetPartSide(args.Part) switch
        {
            BodySide.Left => HandLocation.Left,
            BodySide.Right => HandLocation.Right,
            _ => HandLocation.Middle,
        };

        var handId = $"body{_body.GetPartAddress(args.Part).Path}";
        _hands.AddHand((args.Body, hands), handId, location);
        ent.Comp.HandId = handId;
    }

    [SubscribeLocalEvent]
    private void OnLeftBody(Entity<BodyPartHandComponent> ent, ref BodyPartLeftBodyEvent args)
    {
        if (ent.Comp.HandId == null)
            return;

        _hands.RemoveHand(args.Body.Owner, ent.Comp.HandId);
        ent.Comp.HandId = null;
    }
}

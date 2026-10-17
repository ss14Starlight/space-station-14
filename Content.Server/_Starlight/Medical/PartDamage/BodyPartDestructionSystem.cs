// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Server._Starlight.Medical.Limbs;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.PartDamage;
using Content.Shared.Body.Components;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server._Starlight.Medical.PartDamage;

public sealed partial class BodyPartDestructionSystem : EntitySystem
{
    [Dependency] private BodyPartDamageSystem _partDamage = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private LimbSystem _limbs = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SLBodySystem _body = default!;

    private const float BlownApartOverkill = 0.5f;

    private readonly List<(EntityUid Body, EntityUid Part, bool BlownApart)> _queue = new();

    [SubscribeLocalEvent]
    private void OnIntegrityChanged(Entity<BodyPartDamageComponent> part, ref BodyPartIntegrityChangedEvent args)
    {
        if (args.NewIntegrity > 0 || args.OldIntegrity <= 0)
            return;

        _queue.Add((args.Body, part, args.Overkill >= part.Comp.MaxIntegrity * BlownApartOverkill));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_queue.Count == 0)
            return;

        var queue = _queue.ToArray();
        _queue.Clear();
        foreach (var (body, part, blownApart) in queue)
        {
            if (TerminatingOrDeleted(body)
                || TerminatingOrDeleted(part)
                || !TryComp<SLBodyPartComponent>(part, out var slPart)
                || slPart.Body != body
                || !TryComp<BodyPartDamageComponent>(part, out var damage)
                || damage.Integrity > 0)
                continue;

            Destroy(body, (part, slPart), blownApart);
        }
    }

    public void Destroy(EntityUid body, Entity<SLBodyPartComponent> part, bool blownApart = false)
    {
        switch (part.Comp.Kind)
        {
            case BodyPartType.Torso:
                _popup.PopupEntity(Loc.GetString("body-part-destroyed-torso", ("target", Identity.Entity(body, EntityManager))),
                    body, PopupType.LargeCaution);
                _gibbing.Gib(body);
                break;
            case BodyPartType.Head:
                DestroyHead(body, part);
                break;
            case BodyPartType.Arm or BodyPartType.Hand or BodyPartType.Leg or BodyPartType.Foot or BodyPartType.Tail:
                Sever(body, part, blownApart);
                break;
        }
    }

    private void Sever(EntityUid body, Entity<SLBodyPartComponent> part, bool blownApart)
    {
        var name = Name(part);
        _partDamage.MoveDamageToParent(part);
        if (!Detach(body, part))
            return;

        var message = blownApart ? "body-part-destroyed-blown-apart" : "body-part-destroyed-severed";
        _popup.PopupEntity(Loc.GetString(message, ("target", Identity.Entity(body, EntityManager)), ("part", name)),
            body, PopupType.LargeCaution);

        if (blownApart)
            QueueDel(part);
    }

    private void DestroyHead(EntityUid body, Entity<SLBodyPartComponent> head)
    {
        _partDamage.MoveDamageToParent(head);

        foreach (var child in _body.GetSubtree(head))
        {
            if (HasComp<BrainComponent>(child))
                _body.TryDetachPart(child, Transform(body).Coordinates);
        }

        if (!Detach(body, head))
            return;

        _humanoid.SetLayersVisibility(body, HumanoidVisualLayersExtension.Sublayers(HumanoidVisualLayers.Head), false);
        _popup.PopupEntity(Loc.GetString("body-part-destroyed-head", ("target", Identity.Entity(body, EntityManager))),
            body, PopupType.LargeCaution);
        QueueDel(head);
        _mobState.ChangeMobState(body, MobState.Dead);
    }

    private bool Detach(EntityUid body, Entity<SLBodyPartComponent> part)
    {
        if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid) && TryComp<SLBodyComponent>(body, out var slBody))
        {
            _limbs.Amputatate((body, Transform(body), humanoid, slBody), (part, Transform(part), MetaData(part), part.Comp));
            return part.Comp.Body == null;
        }

        return _body.TryDetachPart(part, Transform(body).Coordinates);
    }
}

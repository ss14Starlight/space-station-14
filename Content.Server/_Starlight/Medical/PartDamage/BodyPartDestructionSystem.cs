// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Server._Starlight.Medical.Limbs;
using Content.Shared._Starlight.Body.Components;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.PartDamage;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Medical.PartDamage;

/// <summary>
/// What happens to a part once its integrity is gone, by the damage that finished it: cuts sever, crushing and
/// gunshots far past the limit blow it apart, burns char it to ash.
/// </summary>
public sealed partial class BodyPartDestructionSystem : EntitySystem
{
    [Dependency] private BodyPartDamageSystem _partDamage = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private LimbSystem _limbs = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SLBodySystem _body = default!;

    private const float BlownApartOverkill = 0.5f;

    private static readonly EntProtoId Ash = "Ash";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";

    private readonly List<(EntityUid Body, EntityUid Part, Destruction How)> _queue = new();

    private enum Destruction : byte
    {
        Severed,
        BlownApart,
        Burned,
    }

    [SubscribeLocalEvent]
    private void OnIntegrityChanged(Entity<BodyPartDamageComponent> part, ref BodyPartIntegrityChangedEvent args)
    {
        if (args.NewIntegrity > 0 || args.OldIntegrity <= 0)
            return;

        var how = args.Cause == BodyPartDamageSystem.HeatType ? Destruction.Burned
            : args.Cause != Slash && args.Overkill >= part.Comp.MaxIntegrity * BlownApartOverkill ? Destruction.BlownApart
            : Destruction.Severed;
        _queue.Add((args.Body, part, how));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_queue.Count == 0)
            return;

        var queue = _queue.ToArray();
        _queue.Clear();
        foreach (var (body, part, how) in queue)
        {
            if (TerminatingOrDeleted(body)
                || TerminatingOrDeleted(part)
                || !TryComp<SLBodyPartComponent>(part, out var slPart)
                || slPart.Body != body
                || !TryComp<BodyPartDamageComponent>(part, out var damage)
                || damage.Integrity > 0)
                continue;

            Destroy(body, (part, slPart), how);
        }
    }

    private void Destroy(EntityUid body, Entity<SLBodyPartComponent> part, Destruction how)
    {
        switch (part.Comp.Kind)
        {
            case BodyPartType.Torso when how == Destruction.Burned:
                Incinerate(body);
                break;
            case BodyPartType.Torso:
                _popup.PopupEntity(Loc.GetString("body-part-destroyed-torso", ("target", Identity.Entity(body, EntityManager))),
                    body, PopupType.LargeCaution);
                _gibbing.Gib(body);
                break;
            case BodyPartType.Head:
                DestroyHead(body, part, how);
                break;
            case BodyPartType.Arm or BodyPartType.Hand or BodyPartType.Leg or BodyPartType.Foot or BodyPartType.Tail:
                Sever(body, part, how);
                break;
        }
    }

    private void Sever(EntityUid body, Entity<SLBodyPartComponent> part, Destruction how)
    {
        var name = Name(part);
        _partDamage.MoveDamageToParent(part);
        if (!Detach(body, part))
            return;

        var message = how switch
        {
            Destruction.Burned => "body-part-destroyed-burned",
            Destruction.BlownApart => "body-part-destroyed-blown-apart",
            _ => "body-part-destroyed-severed",
        };
        _popup.PopupEntity(Loc.GetString(message, ("target", Identity.Entity(body, EntityManager)), ("part", name)),
            body, PopupType.LargeCaution);

        if (how == Destruction.Burned)
            SpawnAtPosition(Ash, Transform(body).Coordinates);
        if (how != Destruction.Severed)
            QueueDel(part);
    }

    private void DestroyHead(EntityUid body, Entity<SLBodyPartComponent> head, Destruction how)
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
        var message = how == Destruction.Burned ? "body-part-destroyed-head-burned" : "body-part-destroyed-head";
        _popup.PopupEntity(Loc.GetString(message, ("target", Identity.Entity(body, EntityManager))),
            body, PopupType.LargeCaution);
        if (how == Destruction.Burned)
            SpawnAtPosition(Ash, Transform(body).Coordinates);
        QueueDel(head);
        _mobState.ChangeMobState(body, MobState.Dead);
    }

    private void Incinerate(EntityUid body)
    {
        _popup.PopupEntity(Loc.GetString("body-part-destroyed-incinerated", ("target", Identity.Entity(body, EntityManager))),
            body, PopupType.LargeCaution);

        foreach (var item in _inventory.GetHandOrInventoryEntities(body))
            _transform.DropNextTo(item, body);

        SpawnAtPosition(Ash, Transform(body).Coordinates);
        QueueDel(body);
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

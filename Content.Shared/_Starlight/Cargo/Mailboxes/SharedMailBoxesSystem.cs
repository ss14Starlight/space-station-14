using System.Linq;
using Content.Shared.Delivery;
using Content.Shared.IdentityManagement;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Cargo.Mailboxes;

/// <summary>
/// This handles the MailBoxesComponent.
/// </summary>
public partial class SharedMailBoxesSystem : EntitySystem
{
    [Dependency] private SharedJobSystem _jobSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MailBoxComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<MailBoxComponent, GetVerbsEvent<InteractionVerb>>(OnInteractionVerbs);
    }

    private void OnInteractionVerbs(Entity<MailBoxComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanComplexInteract) return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb()
        {
            Text = "Get your mail",
            Act = () =>
            {
                EjectMail(ent, user);
            }
        });
    }

    private void EjectMail(Entity<MailBoxComponent> ent, EntityUid argsUser)
    {
        if (!_containerSystem.TryGetContainer(ent, StorageComponent.ContainerId, out var container))
            return;

        var userName = Identity.Name(argsUser, EntityManager);

        foreach (var entity in container.ContainedEntities.ToArray())
        {
            if (!TryComp<DeliveryComponent>(entity, out var delivery))
                continue;

            if (delivery.RecipientName != userName)
                continue;

            _containerSystem.RemoveEntity(ent, entity, reparent: true, force: true);
        }
    }

    private void OnInsertAttempt(Entity<MailBoxComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<DeliveryComponent>(args.EntityUid) || HasComp<DeliveryBombComponent>(args.EntityUid) ||
            HasComp<DeliveryPriorityComponent>(args.EntityUid))
        {
            args.Cancel();
            return;
        }

        var delivery = Comp<DeliveryComponent>(args.EntityUid);
        DepartmentPrototype? department = null;
        if (delivery.RecipientJobTitle != null && !_jobSystem.TryGetDepartment(delivery.RecipientJobTitle, out department) && delivery.RecipientName != null)
        {
            args.Cancel();
            return;
        }
        if (department != null && department != ent.Comp.Department) args.Cancel();

        if (delivery.RecipientName != null) ent.Comp.Names.Add(delivery.RecipientName);
    }
}

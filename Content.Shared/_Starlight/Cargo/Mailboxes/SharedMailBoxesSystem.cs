using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.Delivery;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Cargo.Mailboxes;

/// <summary>
/// This handles the MailBoxesComponent.
/// </summary>
public sealed partial class SharedMailBoxesSystem : EntitySystem
{
    [Dependency] private SharedJobSystem _jobSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MailBoxComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<MailBoxComponent, GetVerbsEvent<InteractionVerb>>(OnInteractionVerbs);
        SubscribeLocalEvent<MailBoxComponent, InteractUsingEvent>(OnInteractWith);
        SubscribeLocalEvent<MailBoxComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MailBoxComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnActivateInWorld(Entity<MailBoxComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex) return;
        args.Handled = true;
        EjectMail(ent, args.User);
    }

    private void OnExamined(Entity<MailBoxComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Names.Contains(GetKeyCardName(args.Examiner))) args.PushMarkup(Loc.GetString("mailbox-has-mail"));
    }

    private void OnInteractWith(Entity<MailBoxComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled) return;
        if(!_containerSystem.TryGetContainer(ent, "mail_storage", out var container)) return;
        args.Handled = _containerSystem.Insert(args.Used, container);
    }

    private void OnInteractionVerbs(Entity<MailBoxComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanComplexInteract) return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("mailbox-get"),
            Act = () => EjectMail(ent, user)
        });
    }

    private void EjectMail(Entity<MailBoxComponent> ent, EntityUid argsUser)
    {
        var userName = GetKeyCardName(argsUser);
        if (!ent.Comp.Names.Contains(userName)) return;
        if (!_containerSystem.TryGetContainer(ent, "mail_storage", out var container))
            return;

        foreach (var entity in container.ContainedEntities.ToArray())
        {
            if (!TryComp<DeliveryComponent>(entity, out var delivery))
                continue;

            if (delivery.RecipientName != userName)
                continue;

            _containerSystem.RemoveEntity(ent, entity, reparent: true, force: true);
        }
        ent.Comp.Names.Remove(userName);
        DirtyField(ent!, nameof(ent.Comp.Names));
    }

    private string GetKeyCardName(EntityUid user)
    {
        if (_idCard.TryFindIdCard(user, out var idCard) && !string.IsNullOrWhiteSpace(idCard.Comp.FullName))
            return idCard.Comp.FullName;

        return "";
    }

    private void OnInsertAttempt(Entity<MailBoxComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || args.Container.ID != "mail_storage")
            return;
        if (!HasComp<DeliveryComponent>(args.EntityUid) || HasComp<DeliveryBombComponent>(args.EntityUid) ||
            HasComp<DeliveryPriorityComponent>(args.EntityUid) || HasComp<DeliveryFragileComponent>(args.EntityUid))
        {
            args.Cancel();
            return;
        }

        var delivery = Comp<DeliveryComponent>(args.EntityUid);
        if (delivery.RecipientJobTitle == null || !_jobSystem.TryGetDepartment(delivery.RecipientJobTitle, out var department) || delivery.RecipientName == null)
        {
            args.Cancel();
            return;
        }
        if (department != ent.Comp.Department) args.Cancel();
        ent.Comp.Names.Add(delivery.RecipientName);
        DirtyField(ent!, nameof(ent.Comp.Names));
    }
}

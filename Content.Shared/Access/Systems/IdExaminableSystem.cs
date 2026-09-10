using Content.Shared._Starlight.IdentityManagement.Components;
using Content.Shared._Starlight.StatusIcon;
using Content.Shared.Access.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Access.Systems;

public sealed partial class IdExaminableSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examineSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    #region Starlight
    [Dependency] private IPrototypeManager _proto = default!; // Starlight
    #endregion

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, IdExaminableComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        var rawInfo = GetInfo(uid); // Starlight

        // Starlight Begin - animals with no ID slot and no fixed job get no verb at all
        if (rawInfo is null && HasComp<AnimalIdentityComponent>(uid))
            return;
        // Starlight End

        var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);
        var info = rawInfo ?? Loc.GetString("id-examinable-component-verb-no-id"); // Starlight

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                var markup = FormattedMessage.FromMarkupOrThrow(info);

                _examineSystem.SendExamineTooltip(args.User, uid, markup, false, false);
            },
            Text = Loc.GetString("id-examinable-component-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("id-examinable-component-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/character.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }

    public string GetMessage(EntityUid uid)
    {
        return GetInfo(uid) ?? Loc.GetString("id-examinable-component-verb-no-id");
    }

    public string? GetInfo(EntityUid uid)
    {
        if (_inventorySystem.TryGetSlotEntity(uid, "id", out var idUid))
        {
            // PDA
            if (TryComp(idUid, out PdaComponent? pda) &&
                TryComp<IdCardComponent>(pda.ContainedId, out var id))
            {
                return GetNameAndJob(id);
            }
            // ID Card
            if (TryComp(idUid, out id))
            {
                return GetNameAndJob(id);
            }
        }

        // Starlight Begin - no ID card slot (K9, Borg, etc); fall back to their fixed job
        if (TryComp<FixedJobIconComponent>(uid, out var fixedJob) && _proto.Resolve(fixedJob.Job, out var job))
        {
            return Loc.GetString("id-examinable-component-verb-fixed-job",
                ("name", MetaData(uid).EntityName),
                ("job", job.LocalizedName));
        }
        // Starlight End

        return null;
    }

    private string GetNameAndJob(IdCardComponent id)
    {
        var jobSuffix = string.IsNullOrWhiteSpace(id.LocalizedJobTitle) ? string.Empty : $" ({id.LocalizedJobTitle})";

        var val = string.IsNullOrWhiteSpace(id.FullName)
            ? Loc.GetString(id.NameLocId,
                ("jobSuffix", jobSuffix))
            : Loc.GetString(id.FullNameLocId,
                ("fullName", id.FullName),
                ("jobSuffix", jobSuffix));

        return val;
    }
}

using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.Localizations;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Blimpuf.Contraband;

/// <summary>
/// Shows tier, type, authorization, and personal clearance details for contraband-marked entities.
/// </summary>
public sealed partial class ContrabandSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedIdCardSystem _id = default!;
    [Dependency] private ExamineSystemShared _examine = default!;

    private bool _contrabandExamineEnabled;
    private bool _contrabandExamineOnlyInHudEnabled;

    public override void Initialize()
    {
        SubscribeLocalEvent<ContrabandComponent, GetVerbsEvent<ExamineVerb>>(OnDetailedExamine);

        Subs.CVar(_configuration, CCVars.ContrabandExamine, SetContrabandExamine, true);
        Subs.CVar(_configuration, CCVars.ContrabandExamineOnlyInHUD, SetContrabandExamineOnlyInHUD, true);
    }

    /// <summary>
    /// Copies type and authorization details from another contraband component.
    /// </summary>
    public void CopyDetails(EntityUid uid, ContrabandComponent other, ContrabandComponent? contraband = null)
    {
        if (!Resolve(uid, ref contraband))
            return;

        contraband.Tier = other.Tier;
        contraband.ContrabandType = other.ContrabandType;
        contraband.AllowedDepartments = other.AllowedDepartments;
        contraband.AllowedJobs = other.AllowedJobs;
        Dirty(uid, contraband);
    }

    private void OnDetailedExamine(EntityUid _, ContrabandComponent component, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!_contrabandExamineEnabled)
            return;

        if (_contrabandExamineOnlyInHudEnabled)
        {
            var ev = new GetContrabandDetailsEvent();
            RaiseLocalEvent(args.User, ref ev);
            if (!ev.CanShowContraband)
                return;
        }

        // CanAccess is not used here because legality should remain examinable from the strip menu.
        if (!args.CanInteract)
            return;

        var classificationMessage = GetClassificationMessage(
            component.Tier,
            component.ContrabandType,
            ContrabandItemType.Item);

        string? authorizationMessage = null;
        if (component.AllowedDepartments.Count > 0 || component.AllowedJobs.Count > 0)
        {
            authorizationMessage = GetAuthorizationMessage(
                component.AllowedDepartments,
                component.AllowedJobs,
                ContrabandItemType.Item);
        }

        var isAuthorized = IsAuthorizedToCarry(args.User, component);
        var carryingMessage = Loc.GetString("contraband-examine-text-avoid-carrying-around");
        var iconTexture = "/Textures/Interface/VerbIcons/lock-red.svg.192dpi.png";
        if (isAuthorized)
        {
            carryingMessage = Loc.GetString("contraband-examine-text-in-the-clear");
            iconTexture = "/Textures/Interface/VerbIcons/unlock-green.svg.192dpi.png";
        }

        var examineMarkup = BuildExamineMessage(classificationMessage, authorizationMessage, carryingMessage);
        _examine.AddHoverExamineVerb(args,
            component,
            Loc.GetString("contraband-examinable-verb-text"),
            examineMarkup.ToMarkup(),
            iconTexture);
    }

    /// <summary>
    /// Builds the localized tier and optional type description shown for contraband.
    /// </summary>
    public string GetClassificationMessage(
        ProtoId<ContrabandTierPrototype> tierId,
        ProtoId<ContrabandTypePrototype>? typeId,
        ContrabandItemType itemType)
    {
        var tier = _proto.Index(tierId);
        var descriptor = Loc.GetString(tier.Descriptor);

        if (typeId is not { } resolvedType)
        {
            return Loc.GetString("contraband-examine-text-tier",
                ("itemType", itemType),
                ("descriptor", descriptor),
                ("tier", tier.Level),
                ("color", tier.Color.ToHex()));
        }

        var contrabandType = Loc.GetString(_proto.Index(resolvedType).Name);
        return Loc.GetString("contraband-examine-text-tier-typed",
            ("itemType", itemType),
            ("descriptor", descriptor),
            ("tier", tier.Level),
            ("contrabandType", contrabandType),
            ("color", tier.Color.ToHex()));
    }

    /// <summary>
    /// Builds the localized list of departments, jobs, and prescription holders authorized to possess contraband.
    /// </summary>
    public string GetAuthorizationMessage(
        HashSet<ProtoId<DepartmentPrototype>> allowedDepartments,
        HashSet<ProtoId<JobPrototype>> allowedJobs,
        ContrabandItemType itemType,
        bool patientsWithPrescription = false)
    {
        var localizedDepartments = allowedDepartments.Select(p =>
            Loc.GetString("contraband-department-plural", ("department", Loc.GetString(_proto.Index(p).Name))));
        var localizedJobs = allowedJobs.Select(p =>
            Loc.GetString("contraband-job-plural", ("job", _proto.Index(p).LocalizedName)));
        var authorizations = localizedDepartments.Concat(localizedJobs).ToList();
        if (patientsWithPrescription)
            authorizations.Add(Loc.GetString("contraband-authorization-prescription-patients"));

        var list = ContentLocalizationManager.FormatList(authorizations);

        return Loc.GetString("contraband-examine-text-restricted",
            ("authorizations", list),
            ("itemType", itemType));
    }

    /// <summary>
    /// Appends reagent classification and authorization to a guidebook entry or visible solution contents.
    /// This is responsible for checking whether the reagent's identity is visible.
    /// </summary>
    public void AppendReagentDescription(FormattedMessage message, ReagentPrototype reagent)
    {
        if (!_contrabandExamineEnabled || reagent.ContrabandTier is not { } tier)
            return;

        message.PushNewline();
        message.AddMarkupPermissive(GetClassificationMessage(tier, reagent.ContrabandType, ContrabandItemType.Reagent));

        if (reagent.AllowedDepartments.Count > 0 || reagent.AllowedJobs.Count > 0 || reagent.RequiresPrescription)
        {
            message.PushNewline();
            message.AddMarkupPermissive(GetAuthorizationMessage(
                reagent.AllowedDepartments,
                reagent.AllowedJobs,
                ContrabandItemType.Reagent,
                reagent.RequiresPrescription));
        }
    }

    private bool IsAuthorizedToCarry(EntityUid user, ContrabandComponent contraband)
    {
        if (!_id.TryFindIdCard(user, out var id))
            return false;

        if (contraband.AllowedDepartments.Overlaps(id.Comp.JobDepartments))
            return true;

        // Examine the actual displayed job title in case someone is not using round start ID.
        var jobTitle = id.Comp.LocalizedJobTitle;
        return !string.IsNullOrEmpty(jobTitle) &&
               contraband.AllowedJobs.Any(job => _proto.Index(job).LocalizedName == jobTitle);
    }

    private static FormattedMessage BuildExamineMessage(
        string classificationMessage,
        string? authorizationMessage,
        string carryingMessage)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(classificationMessage);
        if (authorizationMessage is not null)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(authorizationMessage);
        }

        msg.PushNewline();
        msg.AddMarkupOrThrow(carryingMessage);
        return msg;
    }

    private void SetContrabandExamine(bool val)
    {
        _contrabandExamineEnabled = val;
    }

    private void SetContrabandExamineOnlyInHUD(bool val)
    {
        _contrabandExamineOnlyInHudEnabled = val;
    }
}

/// <summary>
/// The kind of object described by contraband examine localization.
/// </summary>
public enum ContrabandItemType
{
    Item,
    Reagent
}

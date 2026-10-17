using System.Diagnostics.CodeAnalysis;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server._Starlight.Antags.BloodBound.Systems;
using Content.Server._Starlight.Roles;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.Stunnable;
using Content.Shared._Starlight.Antags.BloodBound.Components;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles.Components;
using Content.Shared.Zombies;
using Robust.Server.Player;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class BloodBoundRuleSystem : GameRuleSystem<BloodBoundRuleComponent>
{
    [Dependency] private BloodBoundSystem _bloodBound = default!;
    [Dependency] private IAdminLogManager _adminLogManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoidAppearance = default!; // Starlight
    [Dependency] private AntagSelectionSystem _antagSystem = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private ObjectivesSystem _objectivesSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private RoleSystem _roleSystem = default!;
    [Dependency] private StunSystem _stunSystem = default!;
    [Dependency] private TargetObjectiveSystem _targetObjectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBoundRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);
        SubscribeLocalEvent<BloodBoundComponent, BloodBoundConvertActionEvent>(OnBloodBoundConvert);
        SubscribeLocalEvent<BloodBoundComponent, BloodBoundCheckConvertActionEvent>(OnBloodBoundCheckConvert);
    }

    private void OnObjectivesTextPrepend(Entity<BloodBoundRuleComponent> entity, ref ObjectivesTextPrependEvent args)
    {
        var antags = _antagSystem.GetAntagIdentifiers(entity.Owner);

        foreach (var (mind, sessionData, name) in antags)
        {
            if (!_roleSystem.MindHasRole<BloodBoundRoleComponent>(mind, out var role))
                continue;

            var boundRole = role.Value.Comp2;

            if (boundRole.Partner == null)
                continue;

            if (!_mindSystem.TryGetMind(boundRole.Partner.Value, out _, out var partnerMind)
                || partnerMind.UserId == null)
            {
                args.Text += "\n" + Loc.GetString("blood-bound-round-end-no-mind",
                    ("name", name),
                    ("username", sessionData.UserName),
                    ("partnerName", MetaData(boundRole.Partner.Value).EntityName));

                continue;
            }

            var partnerUsername = _playerManager.GetPlayerData(partnerMind.UserId.Value).UserName;

            args.Text += "\n" + Loc.GetString("blood-bound-round-end",
                ("name", name),
                ("username", sessionData.UserName),
                ("partnerName", MetaData(boundRole.Partner.Value).EntityName),
                ("partnerUsername", (partnerUsername)));
        }
    }

    private void OnBloodBoundConvert(Entity<BloodBoundComponent> entity,
        ref BloodBoundConvertActionEvent args)
    {
        var originalComponent = entity.Comp;

        if (!CanConvert(entity, args.Target, out var failureMessage))
        {
            _popupSystem.PopupEntity(
                Loc.GetString(failureMessage,
                    ("converter", Identity.Entity(entity, _entityManager)),
                    ("converted", Identity.Entity(args.Target, _entityManager))),
                args.Target,
                entity,
                PopupType.MediumCaution);
            return;
        }

        if (!_mindSystem.TryGetMind(entity, out var mindId, out _))
            return;

        if (!_mindSystem.TryGetMind(args.Target, out var targetMindId, out var targetMind))
            return;

        // Actual conversion logic
        var convertedComp = new BloodBoundComponent
        {
            BloodBoundIcon = originalComponent.BloodBoundIcon,
            BloodBoundFaction = originalComponent.BloodBoundFaction,
            EscapeObjective = originalComponent.EscapeObjective,
        };
        AddComp(args.Target, convertedComp);

        _adminLogManager.Add(LogType.Mind,
            LogImpact.Medium,
            $"{ToPrettyString(entity)} converted {ToPrettyString(args.Target)} into their Blood Bound");

        originalComponent.Partner = args.Target;
        if (_roleSystem.MindHasRole<BloodBoundRoleComponent>(mindId, out var role))
            role.Value.Comp2.Partner = args.Target;

        if (!_roleSystem.MindHasRole(targetMindId, out Entity<MindRoleComponent, BloodBoundRoleComponent>? targetRole))
        {
            _roleSystem.MindAddRole(targetMindId, entity.Comp.BloodBoundMindRole, targetMind);
            _roleSystem.MindHasRole(targetMindId, out targetRole);
        }

        DebugTools.AssertNotNull(targetRole, "Blood Bound role was null after assigning it.");

        convertedComp.Partner = entity;
        targetRole!.Value.Comp2.Partner = entity;

        _bloodBound.FinishRecruitment(entity);
        Dirty(entity, originalComponent);
        Dirty(args.Target, convertedComp);

        _mindSystem.TryAddObjective(targetMindId, targetMind, entity.Comp.EscapeObjective);

        if (!_objectivesSystem.TryCreateObjective((targetMindId, targetMind),
                entity.Comp.AssistObjective,
                out var newObjective))
            return;

        var targetObjective = EnsureComp<TargetObjectiveComponent>(newObjective.Value);

        _targetObjectiveSystem.SetTarget(newObjective.Value, mindId, targetObjective);

        _mindSystem.AddObjective(targetMindId, targetMind, newObjective.Value);

        // Visuals
        _antagSystem.SendBriefing(args.Target,
            Loc.GetString(entity.Comp.BriefingText),
            entity.Comp.BriefingColor,
            entity.Comp.BriefingSound);

        _popupSystem.PopupEntity(
            Loc.GetString(
                entity.Comp.ConvertPopupText,
                ("converter", Identity.Entity(entity, _entityManager)),
                ("converted", Identity.Entity(args.Target, _entityManager))),
            args.Target,
            PopupType.LargeCaution);

        if (entity.Comp.ConvertStunTime != null)
            _stunSystem.TryAddParalyzeDuration(args.Target, entity.Comp.ConvertStunTime.Value);

    }

    private void OnBloodBoundCheckConvert(Entity<BloodBoundComponent> entity,
        ref BloodBoundCheckConvertActionEvent args)
    {
        if (!CanConvert(entity, args.Target, out var failureMessage))
        {
            _popupSystem.PopupEntity(
                Loc.GetString(failureMessage,
                    ("converter", Identity.Entity(entity, _entityManager)),
                    ("converted", Identity.Entity(args.Target, _entityManager))),
                args.Target,
                entity,
                PopupType.MediumCaution);
            return;
        }

        _popupSystem.PopupEntity(
            Loc.GetString("blood-bound-convert-convertible",
                ("converter", Identity.Entity(entity, _entityManager)),
                ("converted", Identity.Entity(args.Target, _entityManager))),
            args.Target,
            entity,
            PopupType.Medium);
    }

    private bool CanConvert(
        Entity<BloodBoundComponent> entity,
        EntityUid target,
        [NotNullWhen(false)] out string? errorMessage)
    {
        errorMessage = null;

        if (!entity.Comp.CanRecruit || entity.Comp.Partner != null)
        {
            errorMessage = "blood-bound-convert-failed-bound";
            return false;
        }

        if (!_mindSystem.TryGetMind(entity, out _, out var converterMind))
        {
            DebugTools.Assert("Blood Bound tried to convert but had no mind.");
            Log.Error("Blood Bound tried to convert but had no mind.");
            errorMessage = "blood-bound-convert-failed-no-mind";
            return false; // How would this even happen
        }

        if (!_mindSystem.TryGetMind(target, out var targetMindId, out var targetMind))
        {
            errorMessage = "blood-bound-convert-failed-no-mind";
            return false;
        }

        // Target is already a blood bound
        if (HasComp<BloodBoundComponent>(target))
        {
            errorMessage = "blood-bound-convert-failed-already-bound";
            return false;
        }

        // Stop the blood bound from converting a target.
        foreach (var objective in converterMind.Objectives)
        {
            if (!TryComp<TargetObjectiveComponent>(objective, out var targetObjective))
                continue;

            if (targetObjective.Target != targetMindId)
                continue;

            errorMessage = "blood-bound-convert-failed-target";
            return false;
        }

        if (!HasComp<HumanoidAppearanceComponent>(target))
        {
            errorMessage = "blood-bound-convert-failed-no-mind";
            return false;
        }

        if (HasComp<ZombieComponent>(target))
        {
            errorMessage = "blood-bound-convert-failed-zombie";
            return false;
        }

        if (targetMind.UserId == null)
        {
            errorMessage = "blood-bound-convert-failed-no-mind";
            return false;
        }

        if (entity.Comp.RequiredAntagPreference is {} preference &&
            (_humanoidAppearance.GetBaseProfile(target) is not {} profile || !profile.AntagPreferences.Contains(preference)))
        {
            errorMessage = "blood-bound-convert-failed-preference";
            return false;
        }

        if (!_mobStateSystem.IsAlive(target))
        {
            errorMessage = "blood-bound-convert-failed-dead";
            return false;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            errorMessage = "blood-bound-convert-failed-shielded";
            return false;
        }

        return true;
    }
}

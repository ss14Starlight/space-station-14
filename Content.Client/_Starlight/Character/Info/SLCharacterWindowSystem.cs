using Content.Client._Starlight.Character.Info.UI;
using Content.Shared.DetailExaminable;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.Roles;
using Content.Shared._Starlight.Character.Info.Components;
using Content.Shared._Starlight.Character.Info;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Character.Info;

/// <summary>
/// Supplies character details and opens the local character window.
/// </summary>
public sealed partial class SLCharacterWindowSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedJobSystem _jobs = default!;

    private EntityQuery<DetailExaminableComponent> _detailExaminableQuery;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidProfileQuery;
    private EntityQuery<MindComponent> _mindQuery;
    private EntityQuery<MindContainerComponent> _mindContainerQuery;
    private EntityQuery<PotentialObjectivesComponent> _potentialObjectivesQuery;

    private void OnRoleTypeChanged(MindRoleTypeChangedEvent ev, EntitySessionEventArgs _) => _ui.GetUIController<CharacterUIController>().UpdateRoleType();

    private void OnOpenCharacterMenu(OpenCharacterMenuEvent ev, EntitySessionEventArgs _) => _ui.GetUIController<CharacterUIController>().OpenWindow();

    public CharacterJobInfo? GetJobInfo(EntityUid entity)
    {
        if (!_jobs.MindTryGetJob(GetMind(entity), out var job))
            return null;

        return new CharacterJobInfo(job.Name, _sprite.Frame0(ProtoMan.Index(job.Icon).Icon));
    }

    public CharacterProfileInfo? GetProfileInfo(EntityUid entity)
    {
        if (!_humanoidProfileQuery.TryComp(entity, out var profile))
            return null;

        var species = string.IsNullOrWhiteSpace(profile.CustomSpecieName)
            ? Loc.GetString(ProtoMan.Index(profile.Species).Name)
            : profile.CustomSpecieName;
        return new CharacterProfileInfo(profile.Gender, profile.Age, species);
    }

    public string? GetDescription(EntityUid entity)
    {
        if (TryComp<CharacterDescriptionComponent>(entity, out var description) &&
            !string.IsNullOrWhiteSpace(description.Description))
        {
            // Character profile text is plain text, even if it contains markup characters.
            return FormattedMessage.EscapeText(FormattedMessage.RemoveMarkupPermissive(description.Description));
        }

        return _detailExaminableQuery.TryComp(entity, out var details) ? details.Content : null;
    }

    public CharacterRoleTypeInfo? GetRoleType(EntityUid? entity)
    {
        if (!_mindQuery.TryComp(GetMind(entity), out var mind) ||
            !ProtoMan.Resolve(mind.RoleType, out var roleType))
            return null;

        return new CharacterRoleTypeInfo(
            mind.Subtype ?? roleType.Faction ?? "role-type-free-agent-name",
            mind.SubtypeColor ?? roleType.FactionColor ?? roleType.Color,
            roleType.Alignment ?? roleType.Name,
            roleType.Color);
    }

    public bool CanPickObjectives(EntityUid? entity) => _potentialObjectivesQuery.TryComp(GetMind(entity), out var potential) && potential.ObjectiveOptions.Count > 0;

    public bool CanPickMoreObjectives(EntityUid? entity) =>
        TryComp<ObjectivePickerProgressComponent>(GetMind(entity), out var progress) && progress.CanPickMore;

    private EntityUid? GetMind(EntityUid? entity) => _mindContainerQuery.TryComp(entity, out var container) ? container.Mind : null;

    public readonly record struct CharacterJobInfo(LocId Name, Texture Icon);

    public readonly record struct CharacterProfileInfo(Gender Gender, int Age, string Species);

    public readonly record struct CharacterRoleTypeInfo(LocId Name, Color Color, LocId Alignment, Color AlignmentColor);

    public override void Initialize()
    {
        base.Initialize();

        _detailExaminableQuery = GetEntityQuery<DetailExaminableComponent>();
        _humanoidProfileQuery = GetEntityQuery<HumanoidAppearanceComponent>();
        _mindQuery = GetEntityQuery<MindComponent>();
        _mindContainerQuery = GetEntityQuery<MindContainerComponent>();
        _potentialObjectivesQuery = GetEntityQuery<PotentialObjectivesComponent>();

        SubscribeNetworkEvent<MindRoleTypeChangedEvent>(OnRoleTypeChanged);
        SubscribeNetworkEvent<OpenCharacterMenuEvent>(OnOpenCharacterMenu);
    }
}

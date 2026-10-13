using Content.Shared._Moffstation.CharacterMenu;
using Content.Shared._Moffstation.Objectives;
using Content.Shared._Starlight.Character.Info.Components;
using Content.Shared.DetailExaminable;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Client._Moffstation.CharacterMenu;

public sealed partial class MoffCharacterWindowSystem
{
    [Dependency] private SharedJobSystem _jobs = default!;

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

    private string SLGetSpeciesName(HumanoidAppearanceComponent profile) => string.IsNullOrWhiteSpace(profile.CustomSpecieName)
            ? Loc.GetString(ProtoMan.Index(profile.Species).Name)
            : profile.CustomSpecieName;

    private string? SLGetCharacterDescription(EntityUid entity)
    {
        if (!TryComp<CharacterDescriptionComponent>(entity, out var description) ||
            string.IsNullOrWhiteSpace(description.Description))
            return null;

        // Character profile text is plain text, even if it contains markup characters.
        return FormattedMessage.EscapeText(FormattedMessage.RemoveMarkupPermissive(description.Description));
    }

    private CharacterRoleTypeInfo? GetFactionAndAlignment(EntityUid? entity)
    {
        if (!_mindQuery.TryComp(GetMind(entity), out var mind) ||
            !ProtoMan.Resolve(mind.RoleType, out var roleType))
            return null;

        return new CharacterRoleTypeInfo(
            mind.Subtype ?? roleType.Faction ?? "role-type-free-agent-name",
            mind.SubtypeColor ?? roleType.Color,
            roleType.Alignment ?? roleType.Name,
            roleType.Color);
    }
}

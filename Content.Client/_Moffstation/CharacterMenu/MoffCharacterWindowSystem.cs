using Content.Shared._Moffstation.CharacterMenu;
using Content.Shared._Moffstation.Objectives;
using Content.Shared.DetailExaminable;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Moffstation.CharacterMenu;

/// <summary>
/// Joe Biden please help me open this window
/// </summary>
public sealed partial class MoffCharacterWindowSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    #region Starlight
    //not dependencies
    private EntityQuery<DetailExaminableComponent> _detailExaminableQuery;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidProfileQuery;
    private EntityQuery<MindComponent> _mindQuery;
    private EntityQuery<MindContainerComponent> _mindContainerQuery;
    private EntityQuery<PotentialObjectivesComponent> _potentialObjectivesQuery;
    #endregion

    // Starlight, no subscription needed here since we initialize now
    private void OnRoleTypeChanged(MindRoleTypeChangedEvent ev, EntitySessionEventArgs _) => _ui.GetUIController<CharacterUIController>().UpdateRoleType();

    // Starlight
    private void OnOpenCharacterMenu(OpenCharacterMenuEvent ev, EntitySessionEventArgs _) => _ui.GetUIController<CharacterUIController>().OpenWindow();

    public CharacterJobInfo? GetJobInfo(EntityUid entity) // Starlight, job title, not prototype ID.
    {
        if (!_jobs.MindTryGetJob(GetMind(entity), out var job)) // Starlight, get job from mind
            return null;

        return new CharacterJobInfo(job.Name, _sprite.Frame0(ProtoMan.Index(job.Icon).Icon));
    }

    public CharacterProfileInfo? GetProfileInfo(EntityUid entity)
    {
        if (!_humanoidProfileQuery.TryComp(entity, out var profile))
            return null;

        return new CharacterProfileInfo(profile.Gender, profile.Age, SLGetSpeciesName(profile)); // Starlight, custom species names.
    }

    public string? GetDescription(EntityUid entity) => SLGetCharacterDescription(entity) ?? // Starlight profile descriptions.
            (_detailExaminableQuery.TryComp(entity, out var description) ? description.Content : null);

    public CharacterRoleTypeInfo? GetRoleType(EntityUid? entity) => GetFactionAndAlignment(entity); // Starlight

    public bool CanPickObjectives(EntityUid? entity) => _potentialObjectivesQuery.TryComp(GetMind(entity), out var potential) && potential.ObjectiveOptions.Count > 0; // Starlight, ignore offers already consumed before deferred component removal.

    private EntityUid? GetMind(EntityUid? entity) => _mindContainerQuery.TryComp(entity, out var container) ? container.Mind : null;

    public readonly record struct CharacterJobInfo(LocId Name, Texture Icon);

    public readonly record struct CharacterProfileInfo(Gender Gender, int Age, string Species); // Starlight

    public readonly record struct CharacterRoleTypeInfo(LocId Name, Color Color, LocId Alignment, Color AlignmentColor); // Starlight
}

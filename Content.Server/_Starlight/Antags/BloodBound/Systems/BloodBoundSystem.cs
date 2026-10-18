using Content.Shared._Starlight.Antags.BloodBound.Components;
using Content.Shared._Starlight.Antags.BloodBound.Systems;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared.Actions;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Antags.BloodBound.Systems;

public sealed partial class BloodBoundSystem : SharedBloodBoundSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedLanguageSystem _language = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;

    private static readonly ProtoId<NpcFactionPrototype> CrewFaction = "NanoTrasen";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodBoundComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BloodBoundComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<BloodBoundComponent> entity, ref MapInitEvent args)
    {
        _language.AddLanguage(entity.Owner, "Darktongue");

        _npcFaction.RemoveFaction(entity.Owner, CrewFaction);
        _npcFaction.AddFaction(entity.Owner, entity.Comp.BloodBoundFaction);

        if (!entity.Comp.CanRecruit || entity.Comp.Partner != null)
            return;

        _actions.AddAction(entity, ref entity.Comp.ConvertActionEntity, entity.Comp.ConvertAction);
        _actions.AddAction(entity, ref entity.Comp.CheckConvertActionEntity, entity.Comp.CheckConvertAction);
    }

    private void OnShutdown(Entity<BloodBoundComponent> entity, ref ComponentShutdown args)
    {
        FinishRecruitment(entity);
    }

    /// <summary>
    /// Remove recruitment actions after forming an oath or removing the role.
    /// </summary>
    public void FinishRecruitment(Entity<BloodBoundComponent> entity)
    {
        entity.Comp.CanRecruit = false;
        _actions.RemoveAction(entity.Comp.ConvertActionEntity);
        _actions.RemoveAction(entity.Comp.CheckConvertActionEntity);
        entity.Comp.ConvertActionEntity = null;
        entity.Comp.CheckConvertActionEntity = null;
    }
}

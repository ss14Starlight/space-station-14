using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Enums;

namespace Content.Shared._Starlight.Xenobiology.Potions;

public sealed class SlimeGenderChangePotionSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _sharedHumanoidAppearanceSystem = default!;
    [Dependency] private readonly SharedPopupSystem _sharedPopupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeGenderChangePotionComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SlimeGenderChangePotionComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
    }

    private void OnAfterInteract(Entity<SlimeGenderChangePotionComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Target.HasValue || !args.CanReach) return;
        args.Handled = true;
        if (!_entityManager.TryGetComponent<HumanoidAppearanceComponent>(args.Target.Value,
                out var humanoidAppearanceComponent)) return;
        if (!ent.Comp.Gender.HasValue)
        {
            _sharedPopupSystem.PopupPredicted("Please select a gender first.", args.User, args.User);
            return;
        }

        if (ent.Comp.Gender.Value == humanoidAppearanceComponent.Gender)
        {
            _sharedPopupSystem.PopupPredicted($"Target's gender is already {ent.Comp.Gender.Value}.", args.User, args.User);
            return;
        }
        _sharedHumanoidAppearanceSystem.SetGender((args.Target.Value, humanoidAppearanceComponent), ent.Comp.Gender.Value);
        _sharedPopupSystem.PopupPredicted($"Target's gender set to {ent.Comp.Gender.Value}.", args.User, args.User);
        PredictedQueueDel(args.Used);
    }

    private void OnGetVerbs(Entity<SlimeGenderChangePotionComponent> entity, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var setGenderCategory = new VerbCategory(Loc.GetString("comp-gender-change-potion-category"), null, false);

        var setNeuterVerb = new InteractionVerb();
        setNeuterVerb.Text = Loc.GetString("comp-gender-change-potion-neuter");
        setNeuterVerb.Act = () => entity.Comp.Gender = Gender.Neuter;
        setNeuterVerb.Disabled = entity.Comp.Gender == Gender.Neuter;
        setNeuterVerb.Message = setNeuterVerb.Disabled
            ? Loc.GetString("comp-gender-change-potion-neuter-set")
            : Loc.GetString("comp-gender-change-potion-neuter-set-already");
        setNeuterVerb.Category = setGenderCategory;

        var setEpiceneVerb = new InteractionVerb();
        setEpiceneVerb.Text = Loc.GetString("comp-gender-change-potion-epicene");
        setEpiceneVerb.Act = () => entity.Comp.Gender = Gender.Epicene;
        setEpiceneVerb.Disabled = entity.Comp.Gender == Gender.Epicene;
        setEpiceneVerb.Message = setEpiceneVerb.Disabled
            ? Loc.GetString("comp-gender-change-potion-epicene-set")
            : Loc.GetString("comp-gender-change-potion-epicene-set-already");
        setEpiceneVerb.Category = setGenderCategory;

        var setFemaleVerb = new InteractionVerb();
        setFemaleVerb.Text = Loc.GetString("comp-gender-change-potion-female");
        setFemaleVerb.Act = () => entity.Comp.Gender = Gender.Female;
        setFemaleVerb.Disabled = entity.Comp.Gender == Gender.Female;
        setFemaleVerb.Message = setFemaleVerb.Disabled
            ? Loc.GetString("comp-gender-change-potion-female-set")
            : Loc.GetString("comp-gender-change-potion-female-set-already");
        setFemaleVerb.Category = setGenderCategory;

        var setMaleVerb = new InteractionVerb();
        setMaleVerb.Text = Loc.GetString("comp-gender-change-potion-male");
        setMaleVerb.Act = () => entity.Comp.Gender = Gender.Male;
        setMaleVerb.Disabled = entity.Comp.Gender == Gender.Male;
        setMaleVerb.Message = setMaleVerb.Disabled
            ? Loc.GetString("comp-gender-change-potion-male-set")
            : Loc.GetString("comp-gender-change-potion-male-set-already");
        setMaleVerb.Category = setGenderCategory;

        args.Verbs.Add(setNeuterVerb);
        args.Verbs.Add(setEpiceneVerb);
        args.Verbs.Add(setFemaleVerb);
        args.Verbs.Add(setMaleVerb);
    }
}

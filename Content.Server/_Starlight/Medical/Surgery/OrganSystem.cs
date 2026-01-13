using System.Linq;
using Content.Server._Starlight.Language;
using Content.Server.Humanoid;
using Content.Shared._Starlight.Language.Components.Translators;
using Content.Shared.CollectiveMind;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Radio.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Content.Shared.Tag;
using Content.Shared.VentCraw;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Medical.Surgery;
public sealed partial class OrganSystem : EntitySystem
{

    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearanceSystem = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedCollectiveMindSystem _collectiveMind = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FunctionalOrganComponent, SurgeryOrganImplantationCompleted>(OnFunctionalOrganImplanted);
        SubscribeLocalEvent<FunctionalOrganComponent, SurgeryOrganExtracted>(OnFunctionalOrganExtracted);

        SubscribeLocalEvent<TaggedOrganComponent, SurgeryOrganImplantationCompleted>(OnTaggedOrganImplanted);
        SubscribeLocalEvent<TaggedOrganComponent, SurgeryOrganExtracted>(OnTaggedOrganExtracted);

        SubscribeLocalEvent<OrganEyesComponent, SurgeryOrganImplantationCompleted>(OnEyeImplanted);
        SubscribeLocalEvent<OrganEyesComponent, SurgeryOrganExtracted>(OnEyeExtracted);

        SubscribeLocalEvent<OrganTongueComponent, SurgeryOrganImplantationCompleted>(OnTongueImplanted);
        SubscribeLocalEvent<OrganTongueComponent, SurgeryOrganExtracted>(OnTongueExtracted);

        SubscribeLocalEvent<AbductorOrganComponent, SurgeryOrganImplantationCompleted>(OnAbductorOrganImplanted);
        SubscribeLocalEvent<AbductorOrganComponent, SurgeryOrganExtracted>(OnAbductorOrganExtracted);

        SubscribeLocalEvent<DamageableComponent, SurgeryOrganImplantationCompleted>(OnOrganImplanted);
        SubscribeLocalEvent<DamageableComponent, SurgeryOrganExtracted>(OnOrganExtracted);

        SubscribeLocalEvent<OrganVisualizationComponent, SurgeryOrganImplantationCompleted>(OnVisualizationImplanted);
        SubscribeLocalEvent<OrganVisualizationComponent, SurgeryOrganExtracted>(OnVisualizationExtracted);
    }

    //

    private void OnFunctionalOrganImplanted(Entity<FunctionalOrganComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        foreach (var comp in (ent.Comp.Components ?? []).Values)
        {
            if (!EntityManager.HasComponent(args.Body, comp.Component.GetType()))
            {
                EntityManager.AddComponent(args.Body, comp.Component);
                UpdateEntity(args.Body, comp.Component, ent.Owner);
            }
        }
    }

    private void OnFunctionalOrganExtracted(Entity<FunctionalOrganComponent> ent, ref SurgeryOrganExtracted args)
    {
        foreach (var comp in (ent.Comp.Components ?? []).Values)
        {
            if (EntityManager.HasComponent(args.Body, comp.Component.GetType()))
            {
                EntityManager.RemoveComponent(args.Body, EntityManager.GetComponent(args.Body, comp.Component.GetType()));
                UpdateEntity(args.Body, comp.Component, ent.Owner);
            }
        }
    }

    private void UpdateEntity(EntityUid ent, IComponent comp, EntityUid? implant = null)
    {
        //For all those components where the enity needs to be updated in their own way after adding or removing a component
        switch (comp)
        {
            case IntrinsicTranslatorComponent _:
                _language.UpdateEntityLanguages(ent);
                break;
            case TaggedOrganComponent _: //Handle any required updates after tagging here
                if(TryComp(ent, out CollectiveMindComponent? collectiveMindComp))
                    _collectiveMind.UpdateCollectiveMind(ent,collectiveMindComp);
                break;
            case EncryptionKeyHolderComponent encrypt: //Move encryption keys between implant and body
                if(implant != null)
                    if(TryComp(implant, out EncryptionKeyHolderComponent? implantKeyHolder))
                        if (TryComp(ent, out EncryptionKeyHolderComponent? bodyKeyHolder))
                            foreach (var key in implantKeyHolder.KeyContainer.ContainedEntities.ToList())
                                _container.Insert(key, bodyKeyHolder.KeyContainer);
                        else
                            foreach (var key in encrypt.KeyContainer.ContainedEntities.ToList())
                                _container.Insert(key, implantKeyHolder.KeyContainer);
                break;
        }
    }

    //

    private void OnTaggedOrganImplanted(Entity<TaggedOrganComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if(ent.Comp.AddTags.Count > 0)
            _tag.AddTags(args.Body, ent.Comp.AddTags);
        if(ent.Comp.RemoveTags.Count > 0)
            _tag.RemoveTags(args.Body, ent.Comp.RemoveTags);
        UpdateEntity(args.Body, ent.Comp);
    }

    private void OnTaggedOrganExtracted(Entity<TaggedOrganComponent> ent, ref SurgeryOrganExtracted args)
    {
        if(ent.Comp.AddTags.Count > 0)
            _tag.RemoveTags(args.Body, ent.Comp.AddTags);
        if(ent.Comp.RemoveTags.Count > 0)
            _tag.AddTags(args.Body, ent.Comp.RemoveTags);
        UpdateEntity(args.Body, ent.Comp);
    }

    //

    private void OnOrganImplanted(Entity<DamageableComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!TryComp<DamageableComponent>(args.Body, out var bodyDamageable)) return;

        var change = _damageableSystem.ChangeDamage(args.Body, ent.Comp.Damage, true, false);
        if (change is not null)
            _damageableSystem.ChangeDamage(ent.Owner, change.Invert(), true, false);
    }
    private void OnOrganExtracted(Entity<DamageableComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (!TryComp<OrganDamageComponent>(ent.Owner, out var damageRule)
         || damageRule.Damage is null
         || !TryComp<DamageableComponent>(args.Body, out var bodyDamageable)) return;

        var change = _damageableSystem.ChangeDamage(args.Body, damageRule.Damage.Invert(), true, false);
        if (change is not null)
            _damageableSystem.ChangeDamage(ent.Owner, change.Invert(), true, false);
    }

    //

    private void OnAbductorOrganImplanted(Entity<AbductorOrganComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (TryComp<AbductorVictimComponent>(args.Body, out var victim))
            victim.Organ = ent.Comp.Organ;
        if (ent.Comp.Organ == AbductorOrganType.Vent)
            AddComp<VentCrawlerComponent>(args.Body);
    }
    private void OnAbductorOrganExtracted(Entity<AbductorOrganComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (TryComp<AbductorVictimComponent>(args.Body, out var victim))
            if (victim.Organ == ent.Comp.Organ)
                victim.Organ = AbductorOrganType.None;

        if (ent.Comp.Organ == AbductorOrganType.Vent)
            RemComp<VentCrawlerComponent>(args.Body);
    }

    //

    private void OnTongueImplanted(Entity<OrganTongueComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (HasComp<AbductorComponent>(args.Body) || !ent.Comp.IsMuted) return;
        RemComp<MutedComponent>(args.Body);
    }

    private void OnTongueExtracted(Entity<OrganTongueComponent> ent, ref SurgeryOrganExtracted args)
    {
        ent.Comp.IsMuted = HasComp<MutedComponent>(args.Body);
        AddComp<MutedComponent>(args.Body);
    }

    //

    private void OnEyeExtracted(Entity<OrganEyesComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (!TryComp<BlindableComponent>(args.Body, out var blindable)) return;

        ent.Comp.EyeDamage = blindable.EyeDamage;
        ent.Comp.MinDamage = blindable.MinDamage;
        _blindable.UpdateIsBlind((args.Body, blindable));
    }
    private void OnEyeImplanted(Entity<OrganEyesComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!TryComp<BlindableComponent>(args.Body, out var blindable)) return;

        _blindable.SetMinDamage((args.Body, blindable), ent.Comp.MinDamage ?? 0);
        _blindable.AdjustEyeDamage((args.Body, blindable), (ent.Comp.EyeDamage ?? 0) - blindable.MaxDamage);
    }

    //

    private void OnVisualizationExtracted(Entity<OrganVisualizationComponent> ent, ref SurgeryOrganExtracted args)
        => _humanoidAppearanceSystem.SetLayersVisibility(args.Body, [ent.Comp.Layer], false);
    private void OnVisualizationImplanted(Entity<OrganVisualizationComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(args.Body, out var _)) return;
        
        _humanoidAppearanceSystem.SetLayersVisibility(args.Body, [ent.Comp.Layer], true);
        _humanoidAppearanceSystem.SetBaseLayerId(args.Body, ent.Comp.Layer, ent.Comp.Prototype);
    }
}

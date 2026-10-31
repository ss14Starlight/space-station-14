// Starlight: Moved to shared.

using Content.Shared._Starlight.Light.Components;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Light.EntitySystems;

[UsedImplicitly]
public sealed partial class SharedExpendableLightSystem : EntitySystem
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedStackSystem _stackSystem = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExpendableLightComponent, ComponentInit>(OnExpLightInit);
        SubscribeLocalEvent<ExpendableLightComponent, ComponentShutdown>(OnExpLightShutdown);
        SubscribeLocalEvent<ExpendableLightComponent, UseInHandEvent>(OnExpLightUse);
        SubscribeLocalEvent<ExpendableLightComponent, GetVerbsEvent<ActivationVerb>>(AddIgniteVerb);
        SubscribeLocalEvent<ExpendableLightComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ExpendableLightComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }

    public override void Update(float frameTime)
    {
        // Starlight: check every tick (no accumulator throttle) so the deadline comparison fires on the same
        // synced tick on client and server. A per-side accumulator desynced the Fading->Dead transition and
        // the client's misprediction briefly re-enabled the light -> a flicker as it went out.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ExpendableLightComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (!light.Activated)
                continue;

            UpdateLight((uid, light), now);
        }
    }

    private void UpdateLight(Entity<ExpendableLightComponent> ent, TimeSpan now)
    {
        var component = ent.Comp;
        if (!component.Activated || now < component.NextStateTransition)
            return;

        switch (component.CurrentState)
        {
            case ExpendableLightState.Lit:
                component.CurrentState = ExpendableLightState.Fading;
                component.NextStateTransition = now + component.FadeOutDuration;
                Dirty(ent);

                UpdateVisualizer(ent);

                break;

            default:
            case ExpendableLightState.Fading:
                component.CurrentState = ExpendableLightState.Dead;
                Dirty(ent);

                _nameModifier.RefreshNameModifiers(ent.Owner);

                _tagSystem.AddTag(ent, TrashTag);

                UpdateSounds(ent);
                UpdateVisualizer(ent);

                if (TryComp<ItemComponent>(ent, out var item))
                {
                    _item.SetHeldPrefix(ent, "unlit", component: item);
                }

                break;
        }
    }

    /// <summary>
    ///     Enables the light if it is not active. Once active it cannot be turned off.
    /// </summary>
    public bool TryActivate(Entity<ExpendableLightComponent> ent, EntityUid? user = null)
    {
        var component = ent.Comp;
        if (!component.Activated && component.CurrentState == ExpendableLightState.BrandNew)
        {
            if (TryComp<ItemComponent>(ent, out var item))
            {
                _item.SetHeldPrefix(ent, "lit", component: item);
            }

            // For botany grown cinnaflares, add to the burn time by adding modifier * plant potency.
            if (TryComp<ProduceComponent>(ent, out var produceComp) &&
                produceComp.PlantData != null && TryComp<PlantComponent>(produceComp.PlantData, out var plantData))
            {
                component.GlowBudget += TimeSpan.FromSeconds(component.PlantBurnTimeModifier * plantData.Potency);
            }

            var ignite = new IgnitionEvent(true);
            RaiseLocalEvent(ent, ref ignite);

            component.CurrentState = ExpendableLightState.Lit;
            component.NextStateTransition = _timing.CurTime + component.GlowBudget;
            Dirty(ent);

            UpdateSounds(ent, user);
            UpdateVisualizer(ent);
        }
        return true;
    }

    private void OnInteractUsing(EntityUid uid, ExpendableLightComponent component, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(args.Used, out StackComponent? stack))
            return;

        if (stack.StackTypeId != component.RefuelMaterialID)
            return;

        // Remaining burn time of the active state (zero/negative once spent).
        var remaining = component.NextStateTransition - _timing.CurTime;
        if (remaining + component.RefuelMaterialTime >= component.RefuelMaximumDuration)
            return;

        if (component.CurrentState is ExpendableLightState.Dead)
        {
            component.CurrentState = ExpendableLightState.BrandNew;
            component.GlowBudget = component.RefuelMaterialTime;
            Dirty(uid, component);

            _nameModifier.RefreshNameModifiers(uid);
            _stackSystem.ReduceCount((args.Used, stack), 1);
            UpdateVisualizer((uid, component));
            return;
        }

        component.NextStateTransition += component.RefuelMaterialTime;
        Dirty(uid, component);
        _stackSystem.ReduceCount((args.Used, stack), 1);
        args.Handled = true;
    }

    private void OnRefreshNameModifiers(Entity<ExpendableLightComponent> entity, ref RefreshNameModifiersEvent args)
    {
        if (entity.Comp.CurrentState is ExpendableLightState.Dead)
            args.AddModifier("expendable-light-spent-prefix");
    }

    private void UpdateVisualizer(Entity<ExpendableLightComponent> ent, AppearanceComponent? appearance = null)
    {
        var component = ent.Comp;
        if (!Resolve(ent, ref appearance, false))
            return;

        _appearance.SetData(ent, ExpendableLightVisuals.State, component.CurrentState, appearance);

        _pointLight.SetEnabled(ent, component.CurrentState is ExpendableLightState.Lit or ExpendableLightState.Fading); // Starlight-edit

        switch (component.CurrentState)
        {
            case ExpendableLightState.Lit:
                _appearance.SetData(ent, ExpendableLightVisuals.Behavior, component.TurnOnBehaviourID, appearance);
                break;

            case ExpendableLightState.Fading:
                _appearance.SetData(ent, ExpendableLightVisuals.Behavior, component.FadeOutBehaviourID, appearance);
                break;

            case ExpendableLightState.Dead:
                _appearance.SetData(ent, ExpendableLightVisuals.Behavior, string.Empty, appearance);
                var ignite = new IgnitionEvent(false);
                RaiseLocalEvent(ent, ref ignite);
                break;
        }
    }

    private void UpdateSounds(Entity<ExpendableLightComponent> ent, EntityUid? user = null)
    {
        var component = ent.Comp;

        switch (component.CurrentState)
        {
            case ExpendableLightState.Lit:
                _audio.PlayPredicted(component.LitSound, ent, user);
                StartLoopedSound(ent, user);
                break;
            case ExpendableLightState.Fading:
                break;
            default:
                _audio.PlayPredicted(component.DieSound, ent, user);
                StopLoopedSound(ent);
                break;
        }

        if (TryComp<ClothingComponent>(ent, out var clothing))
        {
            _clothing.SetEquippedPrefix(ent, component.Activated ? "Activated" : string.Empty, clothing);
        }
    }

    #region Starlight

    private void StartLoopedSound(Entity<ExpendableLightComponent> ent, EntityUid? user)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var comp = ent.Comp;
        if (comp.LoopedSound == null || comp.PlayingStream != null)
            return;

        var loop = comp.LoopedSound.Params.WithLoop(true);
        var stream = _audio.PlayPredicted(comp.LoopedSound, ent, user, loop);
        if (stream?.Entity is { } entity)
            comp.PlayingStream = entity;
    }

    private void StopLoopedSound(Entity<ExpendableLightComponent> ent)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        ent.Comp.PlayingStream = _audio.Stop(ent.Comp.PlayingStream);
    }

    private void OnExpLightShutdown(Entity<ExpendableLightComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.PlayingStream = _audio.Stop(ent.Comp.PlayingStream);
    }
    #endregion

    private void OnExpLightInit(EntityUid uid, ExpendableLightComponent component, ComponentInit args)
    {
        if (TryComp<ItemComponent>(uid, out var item))
        {
            _item.SetHeldPrefix(uid, "unlit", component: item);
        }

        component.CurrentState = ExpendableLightState.BrandNew;
        component.GlowBudget = component.GlowDuration;
    }

    private void OnExpLightUse(Entity<ExpendableLightComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryActivate(ent, args.User)) // Starlight: pass the user so PlayPredicted excludes the predictor and the ignition sound isn't doubled
            args.Handled = true;
    }

    private void AddIgniteVerb(Entity<ExpendableLightComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.CurrentState != ExpendableLightState.BrandNew)
            return;

        var user = args.User;

        // Ignite the flare or make the glowstick glow.
        // Also hot damn, those are some shitty glowsticks, we need to get a refund.
        ActivationVerb verb = new()
        {
            Text = Loc.GetString("expendable-light-start-verb"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/light.svg.192dpi.png")),
            Act = () => TryActivate(ent, user)
        };
        args.Verbs.Add(verb);
    }
}

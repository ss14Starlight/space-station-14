using Content.Server.Chemistry.EntitySystems;
using Content.Server.Forensics;
using Content.Shared._Starlight.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Chemistry;

public sealed partial class ChameleonGlassSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private NameModifierSystem _names = default!;
    [Dependency] private TransformableContainerSystem _transformable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ReactionMixerSystem _mixing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan EmpChangeInterval = TimeSpan.FromSeconds(1f / 7f);
    private static readonly SoundSpecifier StirSound = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");
    private readonly List<string> _validDisguises = new();

    public override void Initialize()
    {
        base.Initialize();
        PrepareDisguises();
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<ChameleonGlassComponent> ent, ref MapInitEvent args)
    {
        // Unique chameleon trace to give detectives something to find
        EnsureComp<ForensicsComponent>(ent.Owner).Residues.Add(Loc.GetString("chameleon-glass-forensic-shards"));

        if (ent.Comp.SelectedReagent != null &&
            _prototypes.TryIndex<ReagentPrototype>(ent.Comp.SelectedReagent, out var reagent) &&
            reagent.MetamorphicSprite != null)
        {
            SetOverride(ent, ent.Comp.SelectedReagent);
            ent.Comp.WasFilledSinceSelection =
                _solutions.TryGetFitsInDispenser(ent.Owner, out _, out var solution) &&
                solution.Contents.Count != 0;
            ent.Comp.ResetOnNextFill = false;
        }
        else
            ent.Comp.SelectedReagent = null;

        _transformable.RefreshDisplayedDescription(ent.Owner);
        _names.RefreshNameModifiers(ent.Owner);
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<ChameleonGlassComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.PendingReagent != null ||
            !TryComp<UtensilComponent>(args.Used, out var utensil) ||
            (utensil.Types & UtensilType.Spoon) == 0 ||
            !HasComp<ReactionMixerComponent>(args.Used))
            return;

        // The bartender will select the appearance, then stir the drink and it will shift
        if (_ui.TryOpenUi(ent.Owner, ChameleonGlassUiKey.Key, args.User))
        {
            ent.Comp.Editor = args.User;
            ent.Comp.Spoon = args.Used;
            args.Handled = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnStirred(Entity<ChameleonGlassComponent> ent, ref ChameleonGlassStirDoAfterEvent args)
    {
        var pending = ent.Comp.PendingReagent;
        ent.Comp.PendingReagent = null;

        if (args.Cancelled || pending == null || args.Used == null ||
            !TryComp<UtensilComponent>(args.Used.Value, out var utensil) ||
            (utensil.Types & UtensilType.Spoon) == 0 ||
            !HasComp<ReactionMixerComponent>(args.Used.Value))
            return;

        // normal stir behavior persists
        _mixing.TryMix(args.Used.Value, ent.Owner);
        SetDisguise(ent, pending);
    }

    [SubscribeLocalEvent]
    private void OnSelected(Entity<ChameleonGlassComponent> ent, ref ChameleonGlassSelectMessage args)
    {
        if (ent.Comp.Editor != args.Actor || ent.Comp.Spoon is not { } spoon ||
            !TryComp<ReactionMixerComponent>(spoon, out var mixer) ||
            !TryComp<UtensilComponent>(spoon, out var utensil) ||
            (utensil.Types & UtensilType.Spoon) == 0 ||
            !_prototypes.TryIndex<ReagentPrototype>(args.ReagentId, out var reagent) ||
            reagent.MetamorphicSprite == null)
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.Actor, mixer.TimeToMix,
            new ChameleonGlassStirDoAfterEvent(), ent.Owner, target: ent.Owner, used: spoon)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BreakOnMove = true
        };
        ent.Comp.PendingReagent = args.ReagentId;
        ent.Comp.Editor = null;
        ent.Comp.Spoon = null;
        _ui.CloseUi(ent.Owner, ChameleonGlassUiKey.Key, args.Actor);

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            ent.Comp.PendingReagent = null;
            return;
        }

        _audio.PlayPvs(mixer.MixingSound ?? StirSound, ent.Owner);
    }

    private void SetDisguise(Entity<ChameleonGlassComponent> ent, string reagentId)
    {
        ent.Comp.SelectedReagent = reagentId;
        ent.Comp.WasFilledSinceSelection =
            _solutions.TryGetFitsInDispenser(ent.Owner, out _, out var solution) &&
            solution.Contents.Count != 0;
        ent.Comp.ResetOnNextFill = false;
        SetOverride(ent, reagentId);
        _transformable.RefreshDisplayedDescription(ent.Owner);
        _names.RefreshNameModifiers(ent.Owner);
        UpdateUi(ent);
    }

    [SubscribeLocalEvent]
    private void OnUiClosed(Entity<ChameleonGlassComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey.Equals(ChameleonGlassUiKey.Key) && ent.Comp.Editor == args.Actor)
        {
            ent.Comp.Editor = null;
            ent.Comp.Spoon = null;
        }
    }

    [SubscribeLocalEvent]
    private void OnSolutionChanged(Entity<ChameleonGlassComponent> ent, ref SolutionChangedEvent args)
    {
        if (ent.Comp.SelectedReagent == null)
            return;

        if (args.Solution.Comp.Solution.Contents.Count == 0)
        {
            if (ent.Comp.WasFilledSinceSelection)
                ent.Comp.ResetOnNextFill = true;
            return;
        }

        if (!ent.Comp.ResetOnNextFill)
        {
            ent.Comp.WasFilledSinceSelection = true;
            return;
        }

        ent.Comp.WasFilledSinceSelection = false;
        ent.Comp.ResetOnNextFill = false;
        ent.Comp.SelectedReagent = null;
        SetOverride(ent, string.Empty);
        _transformable.RefreshDisplayedDescription(ent.Owner);
        _names.RefreshNameModifiers(ent.Owner);
        UpdateUi(ent);
    }

    // Scrabmled by EMP, same as chameleon clothing.
    [SubscribeLocalEvent]
    private void OnEmpPulse(Entity<ChameleonGlassComponent> ent, ref EmpPulseEvent args)
    {
        if (_validDisguises.Count == 0)
            return;

        SetDisguise(ent, _random.Pick(_validDisguises));
        ent.Comp.NextEmpChange = _timing.CurTime + EmpChangeInterval;
        args.Affected = true;
        args.Disabled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EmpDisabledComponent, ChameleonGlassComponent>();
        while (query.MoveNext(out var uid, out _, out var chameleon))
        {
            if (_timing.CurTime < chameleon.NextEmpChange || _validDisguises.Count == 0)
                continue;

            SetDisguise((uid, chameleon), _random.Pick(_validDisguises));
            chameleon.NextEmpChange = _timing.CurTime + EmpChangeInterval;
        }
    }

    [SubscribeLocalEvent]
    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReagentPrototype>())
            PrepareDisguises();
    }

    private void PrepareDisguises()
    {
        _validDisguises.Clear();
        foreach (var reagent in _prototypes.EnumeratePrototypes<ReagentPrototype>())
        {
            if (reagent.MetamorphicSprite != null)
                _validDisguises.Add(reagent.ID);
        }
    }

    private void SetOverride(Entity<ChameleonGlassComponent> ent, string value)
        => _appearance.SetData(ent.Owner, ChameleonGlassVisuals.Override, value);

    private void UpdateUi(Entity<ChameleonGlassComponent> ent)
        => _ui.SetUiState(ent.Owner, ChameleonGlassUiKey.Key, new ChameleonGlassUiState(ent.Comp.SelectedReagent));
}

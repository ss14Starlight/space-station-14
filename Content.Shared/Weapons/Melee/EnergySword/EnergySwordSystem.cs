using Content.Shared.Interaction;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Toggleable;
using Content.Shared.Tools.Systems;
using Robust.Shared.Random;

namespace Content.Shared.Weapons.Melee.EnergySword;

public sealed class EnergySwordSystem : EntitySystem
{
    [Dependency] private readonly SharedRgbLightControllerSystem _rgbSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly IViewVariablesManager _vvm = default!; // Starlight: support character-script blade colour writes.

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnergySwordComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EnergySwordComponent, InteractUsingEvent>(OnInteractUsing);

        // Starlight-start: VV writes must update the blade's appearance as well as its stored colour.
        _vvm.GetTypeHandler<EnergySwordComponent>()
            .AddPath(nameof(EnergySwordComponent.ActivatedColor), (_, comp) => comp.ActivatedColor, SetActivatedColor);
        // Starlight-end
    }

    // Starlight-start
    public override void Shutdown()
    {
        base.Shutdown();

        _vvm.GetTypeHandler<EnergySwordComponent>()
            .RemovePath(nameof(EnergySwordComponent.ActivatedColor));
    }
    // Starlight-end

    // Used to pick a random color for the blade on map init.
    private void OnMapInit(Entity<EnergySwordComponent> entity, ref MapInitEvent args)
    {
        // Starlight-start: use the same colour update for spawning and later VV writes.
        var color = entity.Comp.ColorOptions.Count != 0
            ? _random.Pick(entity.Comp.ColorOptions)
            : entity.Comp.ActivatedColor;

        SetActivatedColor(entity, color, entity.Comp);
        // Starlight-end
    }

    // Starlight-start
    /// <summary>
    /// Updates the blade color and its appearance, including while the sword is switched off.
    /// </summary>
    public void SetActivatedColor(EntityUid uid, Color color, EnergySwordComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.ActivatedColor = color;
        Dirty(uid, component);

        if (!TryComp(uid, out AppearanceComponent? appearanceComponent))
            return;

        _appearance.SetData(uid, ToggleableVisuals.Color, color, appearanceComponent);
    }
    // Starlight-end

    // Used to make the blade multicolored when using a multitool on it.
    private void OnInteractUsing(Entity<EnergySwordComponent> entity, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_toolSystem.HasQuality(args.Used, SharedToolSystem.PulseQuality))
            return;

        args.Handled = true;
        entity.Comp.Hacked = !entity.Comp.Hacked;

        if (entity.Comp.Hacked)
        {
            var rgb = EnsureComp<RgbLightControllerComponent>(entity);
            _rgbSystem.SetCycleRate(entity, entity.Comp.CycleRate, rgb);
        }
        else
            RemComp<RgbLightControllerComponent>(entity);

        Dirty(entity);
    }
}

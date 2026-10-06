using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Interaction;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Tools.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.EnergyColor;

public sealed partial class EnergyColorSystem : EntitySystem
{
    [Dependency] private SharedRgbLightControllerSystem _rgb = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IViewVariablesManager _vvm = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnergyColorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EnergyColorComponent, AfterAutoHandleStateEvent>(OnAfterAutoState);
        SubscribeLocalEvent<EnergyColorComponent, InteractUsingEvent>(OnInteractUsing);

        // Direct VV field writes do not refresh the server's appearance data.
        _vvm.GetTypeHandler<EnergyColorComponent>()
            .AddPath(nameof(EnergyColorComponent.ActiveColor), (_, comp) => comp.ActiveColor, SetActiveColor);
    }

    public override void Shutdown()
    {
        _vvm.GetTypeHandler<EnergyColorComponent>()
            .RemovePath(nameof(EnergyColorComponent.ActiveColor));

        base.Shutdown();
    }

    /// <summary>
    /// Sets <see cref="EnergyColorComponent.ActiveColor"/> and updates its networked appearance.
    /// </summary>
    /// <remarks>
    /// Also updates appearance while inactive so character scripts can recolor newly spawned items.
    /// </remarks>
    public void SetActiveColor(EntityUid uid, Color? color, EnergyColorComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.ActiveColor = color;
        Dirty(uid, comp);
        UpdateAppearance(uid, comp);
    }

    private void OnMapInit(Entity<EnergyColorComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.ColorOptions.Count > 0 && ent.Comp.ActiveColor is null)
            // Technically not required per se, but also no reason *not* to just predict it...
            ent.Comp.ActiveColor = _rand.PickPredicted(_timing, ent.Comp.ColorOptions);
        UpdateAppearance(ent, ent.Comp);
    }

    private void OnAfterAutoState(Entity<EnergyColorComponent> ent, ref AfterAutoHandleStateEvent args) =>
        UpdateAppearance(ent, ent.Comp);

    private void OnInteractUsing(Entity<EnergyColorComponent> ent, ref InteractUsingEvent args)
    {
        if (!ent.Comp.CanHack) return;

        if (ent.Comp.HackingUnlockQuality is not null && _tool.HasQuality(args.Used, ent.Comp.HackingUnlockQuality))
        {
            ent.Comp.HackingLocked = !ent.Comp.HackingLocked;
            Dirty(ent, ent.Comp);

            if (ent.Comp.HackingLockStatePopup is not null)
                _popup.PopupPredicted(Loc.GetString(ent.Comp.HackingLockStatePopup, ("item", MetaData(ent).EntityName), ("state", ent.Comp.HackingLocked ? "activated" : "deactivated")),
                    ent, args.User);
            return;
        }

        if (!_tool.HasQuality(args.Used, ent.Comp.HackingQuality)) return;
        if (ent.Comp.HackingLocked)
        {
            if (ent.Comp.HackingLockedPopup is not null)
                _popup.PopupPredicted(Loc.GetString(ent.Comp.HackingLockedPopup, ("item", MetaData(ent).EntityName)),
                    ent, args.User);
            return;
        }
        ent.Comp.Hacked = !ent.Comp.Hacked;
        Dirty(ent, ent.Comp);

        if (ent.Comp.Hacked)
        {
            var rgb = EnsureComp<RgbLightControllerComponent>(ent);
            _rgb.SetCycleRate(ent, ent.Comp.CycleRate, rgb);
        }
        else RemComp<RgbLightControllerComponent>(ent);
    }

    private void UpdateAppearance(EntityUid uid, EnergyColorComponent comp)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance)) return;
        _appearance.SetData(uid, ToggleableVisuals.Color, comp.ActiveColor ?? Color.White, appearance);

        if (!TryComp<RgbLightControllerComponent>(uid, out var rgb)) return;
        _rgb.SetCycleRate(uid, comp.CycleRate);
    }
}

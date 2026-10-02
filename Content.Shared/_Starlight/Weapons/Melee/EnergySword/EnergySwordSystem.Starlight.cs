using Content.Shared.Toggleable;

namespace Content.Shared.Weapons.Melee.EnergySword;

// Keeps Starlight's character-script blade colour support separate from the upstream sword system.
public sealed partial class EnergySwordSystem
{
    [Dependency] private IViewVariablesManager _vvm = default!;

    private void InitializeStarlight()
    {
        // Changing the stored colour alone would leave the blade's appearance at its spawn colour.
        _vvm.GetTypeHandler<EnergySwordComponent>()
            .AddPath(nameof(EnergySwordComponent.ActivatedColor), (_, comp) => comp.ActivatedColor, SetActivatedColor);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _vvm.GetTypeHandler<EnergySwordComponent>()
            .RemovePath(nameof(EnergySwordComponent.ActivatedColor));
    }

    /// <summary>
    /// Updates the stored blade colour and networked appearance without switching the sword on.
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
}

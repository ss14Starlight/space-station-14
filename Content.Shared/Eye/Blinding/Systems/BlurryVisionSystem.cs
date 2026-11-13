using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory;

namespace Content.Shared.Eye.Blinding.Systems;

public sealed partial class BlurryVisionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisionCorrectionComponent, GotEquippedEvent>(OnGlassesEquipped);
        SubscribeLocalEvent<VisionCorrectionComponent, GotUnequippedEvent>(OnGlassesUnequipped);
        Subs.SubscribeWithRelay<VisionCorrectionComponent, GetBlurEvent>(OnGetBlur, held: false); // Moffstation - Modular HUDs need to handle both the basic event and the inventory relayed one
    }

    // Moffstation - Begin - Modular HUDs need this to be base + relayed
    private void OnGetBlur(Entity<VisionCorrectionComponent> glasses, ref GetBlurEvent args)
    {
        args.HasVisionCorrection = true; // Starlight
        args.Blur += glasses.Comp.VisionBonus;
        args.CorrectionPower *= glasses.Comp.CorrectionPower;
    }
    // Moffstation - End

    public void UpdateBlurMagnitude(Entity<BlindableComponent?> ent, bool glasses) // starlight change: glasses param
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var ev = new GetBlurEvent(ent.Comp.EyeDamage);
        RaiseLocalEvent(ent, ev);

        var blur = Math.Clamp(ev.Blur, 0, BlurryVisionComponent.MaxMagnitude);
        if (blur <= 0)
        {
            RemCompDeferred<BlurryVisionComponent>(ent);
            return;
        }

        ent.Comp.IsWearingGlasses = glasses && ent.Comp.GlassesFixable; // Starlight-edit
		var blurry = EnsureComp<BlurryVisionComponent>(ent);
        blurry.Magnitude = glasses && ent.Comp.GlassesFixable ? 0 : blur; // starlight
        blurry.CorrectionPower = ev.CorrectionPower;
        Dirty(ent, blurry);
    }

    private void OnGlassesEquipped(Entity<VisionCorrectionComponent> glasses, ref GotEquippedEvent args) => UpdateBlurMagnitude(args.EquipTarget);

    private void OnGlassesUnequipped(Entity<VisionCorrectionComponent> glasses, ref GotUnequippedEvent args) => UpdateBlurMagnitude(args.EquipTarget);
}

public sealed class GetBlurEvent : EntityEventArgs, IInventoryRelayEvent
{
    public readonly float BaseBlur;
    public float Blur;
    public float CorrectionPower = BlurryVisionComponent.DefaultCorrectionPower;
    public bool HasVisionCorrection; // Starlight

    public GetBlurEvent(float blur)
    {
        Blur = blur;
        BaseBlur = blur;
    }

    public SlotFlags TargetSlots => SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES;
}

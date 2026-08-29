using Content.Shared.Emp;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio.Components;
using Content.Shared._Starlight.Clothing;
using Robust.Shared.Utility;
using Content.Shared.Examine;

namespace Content.Shared.Radio.EntitySystems;

public abstract class SharedHeadsetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadsetComponent, InventoryRelayedEvent<GetDefaultRadioChannelEvent>>(OnGetDefault);
        SubscribeLocalEvent<HeadsetComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<HeadsetComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<HeadsetComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<HeadsetLoudModeComponent, ExaminedEvent>(OnExamined); // starlight
        SubscribeLocalEvent<HeadsetLoudModeComponent, GetVerbsEvent<Verb>>(GetVerb); // starlight
    }

    private void OnGetDefault(EntityUid uid, HeadsetComponent component, InventoryRelayedEvent<GetDefaultRadioChannelEvent> args)
    {
        if (!component.Enabled || !component.IsEquipped)
        {
            // don't provide default channels from pocket slots.
            return;
        }

        if (TryComp(uid, out EncryptionKeyHolderComponent? keyHolder))
            args.Args.Channel ??= keyHolder.DefaultChannel;
    }

    protected virtual void OnGotEquipped(EntityUid uid, HeadsetComponent component, GotEquippedEvent args)
    {
        component.IsEquipped = args.SlotFlags.HasFlag(component.RequiredSlot);
        Dirty(uid, component);
    }

    protected virtual void OnGotUnequipped(EntityUid uid, HeadsetComponent component, GotUnequippedEvent args)
    {
        component.IsEquipped = false;
        Dirty(uid, component);
    }

    private void OnEmpPulse(Entity<HeadsetComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.Enabled)
        {
            args.Affected = true;
            args.Disabled = true;
        }
    }
    #region Starlight
    private void OnExamined(EntityUid uid, HeadsetLoudModeComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(component.Active ? "headset-loud-mode-examine-active" : "headset-loud-mode-examine-inactive"));
    }

    private void GetVerb(EntityUid uid, HeadsetLoudModeComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract)
            return;

        args.Verbs.Add(new Verb
        {
            Act = () => ToggleLoudMode(uid, component),
            Text = Loc.GetString("ui-verb-toggle-loud-mode"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/_Starlight/Interface/VerbIcons/voice.192dpi.png")),
        });
    }

    private void ToggleLoudMode(EntityUid uid, HeadsetLoudModeComponent component)
    {
        component.Active = !component.Active;
        Dirty(uid, component);
    }
    #endregion Starlight
}

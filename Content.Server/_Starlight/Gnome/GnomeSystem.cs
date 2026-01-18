using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Server.Clothing.Systems;
using Content.Server.Emoting.Systems;
using Content.Server.Popups;
using Content.Server.Speech.EntitySystems;
using Content.Shared._Starlight.Gnome;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Gnome;

public sealed partial class GnomeSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private OutfitSystem _outfit = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private AutoEmoteSystem _autoEmote = default!;
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GnomeComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<GnomeComponent, EmoteEvent>(OnEmote, before: new[] { typeof(VocalSystem), typeof(BodyEmotesSystem) });

        base.Initialize();
    }

    private void OnInit(Entity<GnomeComponent> ent, ref MapInitEvent args)
    {
        _popup.PopupEntity(Loc.GetString("gnomification", ("target", ent.Owner)), ent.Owner, PopupType.LargeCaution);
        _outfit.SetOutfit(ent.Owner, ent.Comp.OutfitName);

        if(!TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoidAppearance)) return;
        humanoidAppearance.Height = ent.Comp.NewHeight; // todo rework when actual height and weight system

        _audio.PlayPvs(ent.Comp.GnomeSound, ent.Owner);

        EnsureComp<AutoEmoteComponent>(ent.Owner);
        _autoEmote.AddEmote(ent.Owner, ent.Comp.AutoEmote);
    }

    private void OnEmote(Entity<GnomeComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled) return;

        // endless heheha
        _audio.PlayPvs(ent.Comp.GnomeSound, ent.Owner);
        _chat.TrySendInGameICMessage(ent.Owner, "giggles", InGameICChatType.Emote, ChatTransmitRange.Normal);
        args.Handled = true;
    }
}
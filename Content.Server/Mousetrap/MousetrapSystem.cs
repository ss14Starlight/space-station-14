using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Mousetrap;
using Content.Shared.StepTrigger.Systems;

namespace Content.Server.Mousetrap;

/// <summary>
/// Server-side mousetrap system for handling chat messages when cats avoid mousetraps.
/// </summary>
public sealed class MousetrapSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MousetrapComponent, MousetrapAvoidedEvent>(OnMousetrapAvoided);
    }

    // Starlight-start: show emote when cat avoids mousetrap
    private void OnMousetrapAvoided(Entity<MousetrapComponent> ent, ref MousetrapAvoidedEvent args)
    {
        var message = Loc.GetString("mousetrap-cat-avoid");
        _chat.TrySendInGameICMessage(args.Tripper, message, InGameICChatType.Emote, ChatTransmitRange.Normal, hideLog: true);
    }
    // Starlight-end
}

using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Events;
using Content.Shared._Starlight.Language.Systems;
using Content.Client._Starlight.BodyControl;
using Robust.Client.Player;

namespace Content.Client._Starlight.Language.Systems;

public sealed partial class LanguageSystem : SharedLanguageSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private BodyControlViewSystem _bodyView = default!;

    /// <summary>
    ///     Invoked when the Languages of the local player entity change, for use in UI.
    /// </summary>
    public event Action? OnLanguagesChanged;

    public override void Initialize()
    {
        base.Initialize();

        _player.LocalPlayerAttached += NotifyUpdate;

        SubscribeLocalEvent<LanguageSpeakerComponent, AfterAutoHandleStateEvent>(OnSpeakerState);
    }

    private void OnSpeakerState(Entity<LanguageSpeakerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner == _bodyView.Entity)
            NotifyUpdate(ent);
    }

    public override LanguageSpeakerComponent? GetLocalSpeaker()
        => CompOrNull<LanguageSpeakerComponent>(_bodyView.Entity);

    [SubscribeLocalEvent]
    private void OnControlViewChanged(BodyControlViewChangedEvent args)
    {
        if (args.Current is { } current)
            NotifyUpdate(current);
    }

    private void NotifyUpdate(EntityUid localPlayer)
    {
        RaiseLocalEvent(localPlayer, new LanguagesUpdateEvent(), broadcast: true);
        OnLanguagesChanged?.Invoke();
    }
}

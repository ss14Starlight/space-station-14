using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Events;
using Content.Shared._Starlight.Language.Systems;
using Robust.Client.Player;

namespace Content.Client._Starlight.Language.Systems;

public sealed class LanguageSystem : SharedLanguageSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

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
        if (ent.Owner == _player.LocalEntity)
            NotifyUpdate(ent);
    }

    private void NotifyUpdate(EntityUid localPlayer)
    {
        RaiseLocalEvent(localPlayer, new LanguagesUpdateEvent(), broadcast: true);
        OnLanguagesChanged?.Invoke();
    }
}

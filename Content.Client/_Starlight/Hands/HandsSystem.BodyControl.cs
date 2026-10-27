using Content.Client._Starlight.BodyControl;
using Content.Shared._Starlight.BodyControl;

// ReSharper disable CheckNamespace
namespace Content.Client.Hands.Systems;

public sealed partial class HandsSystem
{
    [Dependency] private BodyControlViewSystem _bodyView = default!;

    private EntityUid? HandUiEntity => _bodyView.Entity;

    [SubscribeLocalEvent]
    private void OnControlViewChanged(BodyControlViewChangedEvent args)
    {
        OnPlayerHandsRemoved?.Invoke();
        ReloadHandButtons();
        RefreshControlHand();
    }

    /// <summary>Refreshes the native hand highlight after the player's private selection changes.</summary>
    public void RefreshControlHand()
    {
        if (HandUiEntity is { } player)
            OnPlayerSetActiveHand?.Invoke(GetActiveHand(player));
    }

    private bool TryControlHandClick(string hand)
        => _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.HandClick, hand));

    private bool TryControlHandActivate(string hand)
        => _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.ActivateHand, hand));

    private bool TryControlHandAltActivate(string hand)
        => _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.AltUseHand, hand));
}

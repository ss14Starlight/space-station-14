// ReSharper disable once CheckNamespace
namespace Content.Client.TurretController;

public sealed partial class TurretControllerWindow
{
    public event Action<bool>? OnNeutralTargetingChangedEvent;

    private bool _refreshingNeutralTargeting;

    private void OnTargetNeutralEntitiesToggled()
    {
        if (!_refreshingNeutralTargeting)
            OnNeutralTargetingChangedEvent?.Invoke(TargetNeutralEntitiesCheckBox.Pressed);
    }

    private void RefreshNeutralTargetingControl(bool enabled)
    {
        _refreshingNeutralTargeting = true;
        TargetNeutralEntitiesCheckBox.Pressed = enabled;
        _refreshingNeutralTargeting = false;
        TargetNeutralEntitiesCheckBox.Disabled = IsReadOnly() || !IsLocalPlayerAllowedToInteract();
    }
}

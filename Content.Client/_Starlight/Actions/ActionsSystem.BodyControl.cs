using System.Linq;
using Content.Client._Starlight.BodyControl;
using Content.Shared._Starlight.BodyControl;
using Content.Shared.Actions.Components;
using Content.Shared.CombatMode;
using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Client.Actions;

public sealed partial class ActionsSystem
{
    [Dependency] private BodyControlViewSystem _bodyView = default!;

    public EntityUid? UiEntity => _bodyView.Entity;

    private bool IsUiEntity(EntityUid? entity)
        => UiEntity == entity || _playerManager.LocalEntity == entity;

    private IEnumerable<Entity<ActionComponent>> GetUiActions(EntityUid user)
    {
        var actions = GetActions(user).Where(IsUiAction);
        if (_playerManager.LocalEntity is { } actor && actor != user)
            actions = actions.Concat(GetActions(actor).Where(action => GetEvent(action) is IBodyControlPersonalAction));
        return actions;
    }

    private bool IsUiAction(Entity<ActionComponent> action)
        => action.Comp.AttachedEntity == UiEntity
           && (GetEvent(action) is not IBodyControlPersonalAction || UiEntity == _playerManager.LocalEntity)
           || action.Comp.AttachedEntity == _playerManager.LocalEntity && GetEvent(action) is IBodyControlPersonalAction;

    [SubscribeLocalEvent]
    private void OnControlViewChanged(BodyControlViewChangedEvent args)
    {
        UnlinkAllActions();
        LinkAllActions();
    }

    public bool CheckControlActionAccess(Entity<ActionComponent> action)
    {
        if (!_bodyView.TryGetSource(out _, out var source) || action.Comp.AttachedEntity != source.Body
            || GetEvent(action) is IBodyControlPersonalAction)
            return true;
        var permitted = GetEvent(action) is ToggleCombatActionEvent ? source.Access.Allows("body.interact")
            : source.Access.Allows("body.actions");
        if (permitted && (GetEvent(action) is ToggleCombatActionEvent || source.Access.FullControl || source.Access.Actions == null
            || MetaData(action).EntityPrototype is { } prototype && source.Access.Actions.Contains(prototype.ID)))
            return true;
        _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.Action, action: GetNetEntity(action)));
        return false;
    }

    private bool TryTriggerControlledAction(Entity<ActionComponent> action)
    {
        if (action.Comp.ClientExclusive && _bodyView.TryGetSource(out _, out var source)
            && action.Comp.AttachedEntity == source.Body)
        {
            if (CheckControlActionAccess(action))
                PerformAction(source.Body, action);
            return true;
        }
        return SendControlledAction(action);
    }

    private bool SendControlledAction(Entity<ActionComponent> action, EntityUid? target = null, NetCoordinates? coordinates = null)
        => _bodyView.TryGetSource(out _, out var source) && action.Comp.AttachedEntity == source.Body
           && GetEvent(action) is not IBodyControlPersonalAction
           && _bodyView.Send(new BodyControlCommandEvent(BodyControlCommand.Action,
               target: GetNetEntity(target), coordinates: coordinates, action: GetNetEntity(action)));
}

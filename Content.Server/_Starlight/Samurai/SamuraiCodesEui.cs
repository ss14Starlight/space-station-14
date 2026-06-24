using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared._Starlight.Samurai;

namespace Content.Server._Starlight.Samurai;

public sealed class SamuraiCodesEui : BaseEui
{
    private readonly SamuraiCodesSystem _codesSystem;
    private readonly EntityManager _entMan;
    private readonly IAdminManager _adminManager;

    private List<SamuraiCode> _codes = [];
    private List<SamuraiCode> _sharedCodes = [];
    private readonly ISawmill _sawmill = default!;
    private EntityUid _target;

    public SamuraiCodesEui(SamuraiCodesSystem samuraiCodesSystem, EntityManager entityManager, IAdminManager manager)
    {
        _codesSystem = samuraiCodesSystem;
        _entMan = entityManager;
        _adminManager = manager;
        _sawmill = Logger.GetSawmill("samurai-codes-eui");
    }

    public override EuiStateBase GetNewState()
        => new SamuraiCodesEuiState(_codes, _entMan.GetNetEntity(_target));

    public void UpdateCodes(Entity<SamuraiCodesComponent> ent)
    {
        if (!IsAllowed())
            return;

        _target = ent;
        _codes = ent.Comp.Codes;
        _sharedCodes = [.. _codesSystem.SharedCodes];
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not SamuraiCodesSaveMessage message)
            return;

        if (!IsAllowed())
            return;

        var uid = _entMan.GetEntity(message.Target);
        if (!_entMan.TryGetComponent<SamuraiCodesComponent>(uid, out var comp))
        {
            _sawmill.Warning($"Entity {_entMan.ToPrettyString(uid)} does not have SamuraiCodesComponent!");
            return;
        }

        _codesSystem.SetCodes((uid, comp), message.Codes);
    }

    private bool IsAllowed()
    {
        var adminData = _adminManager.GetAdminData(Player);
        if (adminData == null || !adminData.HasFlag(AdminFlags.Moderator))
        {
            _sawmill.Warning($"Player {Player.UserId} tried to open / use samurai codes UI without permission.");
            return false;
        }

        return true;
    }
}

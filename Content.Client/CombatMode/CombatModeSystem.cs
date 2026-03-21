using Content.Client.Hands.Systems;
using Content.Client.NPC.HTN;
using Content.Shared.CCVar;
using Content.Shared.CombatMode;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Configuration;

#region Starlight
using Content.Shared._Starlight.CombatMode;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Prototypes;
#endregion

namespace Content.Client.CombatMode;

public sealed class CombatModeSystem : SharedCombatModeSystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    #region Starlight
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    #endregion

    /// <summary>
    /// Raised whenever combat mode changes.
    /// </summary>
    public event Action<bool>? LocalPlayerCombatModeUpdated;

    #region Starlight
    private bool _lastState = false;
    private string _rangedSight = "GunSight";
    private string _meleeSight = "MeleeSight";
    private float _scale = 0.6f;
    private float _offset = 0.5f;
    private bool _rotation = true;
    private Color _main = Color.White.WithAlpha(0.3f);
    private Color _second = Color.Black.WithAlpha(0.5f);
    #endregion

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatModeComponent, AfterAutoHandleStateEvent>(OnHandleState);

        Subs.CVar(_cfg, CCVars.CombatModeIndicatorsPointShow, OnShowCombatIndicatorsChanged, true);
        // Starlight-start
        Subs.CVar(_cfg, StarlightCCVars.RangedSight, OnRangedSightChanged, true);
        Subs.CVar(_cfg, StarlightCCVars.RangedSightScale, OnRangedSightScaleChanged, true);
        Subs.CVar(_cfg, StarlightCCVars.RangedSightOffset, OnRangedSightOffsetChanged, true);
        Subs.CVar(_cfg, StarlightCCVars.SightMainColor, OnSightMainColorChanged, true);
        Subs.CVar(_cfg, StarlightCCVars.SightSecondColor, OnSightSecondColorChanged, true);
        Subs.CVar(_cfg, StarlightCCVars.MeleeSight, OnMeleeSightChanged, true);
        Subs.CVar(_cfg, StarlightCCVars.SightRotation, OnRotationChanged, true);
        // Starlight-end
    }

    private void OnHandleState(EntityUid uid, CombatModeComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateHud(uid);
    }

    public override void Shutdown()
    {
        _overlayManager.RemoveOverlay<CombatModeIndicatorsOverlay>();

        base.Shutdown();
    }

    public bool IsInCombatMode()
    {
        var entity = _playerManager.LocalEntity;

        if (entity == null)
            return false;

        return IsInCombatMode(entity.Value);
    }

    public override void SetInCombatMode(EntityUid entity, bool value, CombatModeComponent? component = null)
    {
        base.SetInCombatMode(entity, value, component);
        UpdateHud(entity);
    }

    protected override bool IsNpc(EntityUid uid)
    {
        return HasComp<HTNComponent>(uid);
    }

    private void UpdateHud(EntityUid entity)
    {
        if (entity != _playerManager.LocalEntity || !Timing.IsFirstTimePredicted)
        {
            return;
        }

        var inCombatMode = IsInCombatMode();
        // Starlight-start
        if (!inCombatMode)
            _clyde.SetCursor(null);
        // Starlight-end
        LocalPlayerCombatModeUpdated?.Invoke(inCombatMode);
    }

    private void OnShowCombatIndicatorsChanged(bool isShow) // Starlight-edit: moved into another method
        => UpdateCombatIndicators(isShow);

    #region Starlight

    private void OnRangedSightChanged(string sight)
    {
        _rangedSight = sight;
        UpdateCombatIndicators();
    }

    private void OnRangedSightScaleChanged(int scale)
    {
        float fScale = scale * 0.01f;
        _scale = fScale;
        UpdateCombatIndicators();
    }

    private void OnRangedSightOffsetChanged(int offset)
    {
        float fOffset = offset * 0.01f;
        _offset = fOffset;
        UpdateCombatIndicators();
    }

    private void OnSightMainColorChanged(string color)
    {
        _main = Color.FromHex(color).WithAlpha(0.3f);
        UpdateCombatIndicators();
    }

    private void OnSightSecondColorChanged(string color)
    {
        _second = Color.FromHex(color).WithAlpha(0.5f);
        UpdateCombatIndicators();
    }

    private void OnMeleeSightChanged(string sight)
    {
        _meleeSight = sight;
        UpdateCombatIndicators();
    }

    private void OnRotationChanged(bool rotation)
    {
        _rotation = rotation;
        UpdateCombatIndicators();
    }

    private void UpdateCombatIndicators(bool? isShow = null)
    {
        if (isShow != null && isShow != _lastState)
            _lastState = isShow.Value;

        if ((isShow == null || !_lastState) && _overlayManager.HasOverlay<CombatModeIndicatorsOverlay>())
            _overlayManager.RemoveOverlay<CombatModeIndicatorsOverlay>();
        if ((isShow == null || _lastState) && _prototypeManager.TryIndex<SightPrototype>(_rangedSight, out var ranged) && _prototypeManager.TryIndex<SightPrototype>(_meleeSight, out var melee))
        {
            _overlayManager.AddOverlay(new CombatModeIndicatorsOverlay(
                _inputManager,
                EntityManager,
                _prototypeManager,
                _eye,
                this,
                EntityManager.System<HandsSystem>(),
                _clyde,
                ranged,
                melee,
                _scale,
                _offset,
                _main,
                _second,
                _rotation));
        }
    }

    #endregion
}
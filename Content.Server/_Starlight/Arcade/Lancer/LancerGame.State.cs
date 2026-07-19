using Content.Shared._Starlight.Arcade.Lancer;

namespace Content.Server._Starlight.Arcade.Lancer;

public sealed partial class LancerGame
{
    private sealed class UnitRecord
    {
        public int Id;
        public LancerUnitKind Kind;
        public LancerGridCoord Position = new();
        public int Hp;
        public int MaxHp;
        public int Armor;
        public int Evasion;
        public bool Destroyed;
        public bool Fleeing;
        public bool Hunkered;
        public bool LockedOn;
        public bool Shredded;
        public bool Impaired;
        /// <summary>Catalytic Hammer crit: skip the next enemy activation, then clear.</summary>
        public bool Stunned;
        /// <summary>Hyper-Reflex Overwatch hit: cannot move on next activation.</summary>
        public bool Immobilized;
        public int Tier;
        public bool Veteran;
        public string WeaponId = LancerGame.WeaponGruntRifle;
        public bool WeaponLoaded = true;
        public string SpriteState = string.Empty;
    }

    private enum AiStepKind
    {
        Move,
        Attack,
        EndTurn
    }

    private sealed class AiStep
    {
        public int UnitId;
        public AiStepKind Kind;
        public LancerGridCoord? TargetCell;
        public int TargetUnitId = -1;
    }

    private sealed class PendingResolution
    {
        public float Timer;
        public Action? Apply;
    }

    private enum PendingReactionContext
    {
        None,
        OverwatchMove,
        BraceDamage
    }

    private readonly EntityUid _owner;
    private readonly List<string> _log = new();
    private readonly List<AiStep> _aiQueue = new();
    private readonly HashSet<LancerPlayerAction> _usedActions = new();

    private LancerGamePhase _phase = LancerGamePhase.Briefing;
    private LancerCellState[][] _cells = Array.Empty<LancerCellState[]>();
    private readonly List<UnitRecord> _units = new();
    private int _nextUnitId = 1;

    private bool _playerTurn = true;
    private bool _spotBonus;
    private bool _spotFailed;
    private int _spotRoll;
    private bool _spotRolled;
    private bool _combatStarted;
    private bool _firstPlayerAttackBonus;

    private int _playerHp = 14;
    private int _structure = 4;
    private int _stress = PlayerMaxStress;
    private int _heat;
    private int _repairs = 6;
    private int _hexCharges = PlayerHexCharges;
    private int _corePower;
    private bool _coreActive;
    private bool _disengaged;
    private bool _holdAndLock;
    private bool _freeBoostUsed;

    private int _moveRemaining;
    private int _quickActionsUsed;
    private const int BaseQuickActionsPerTurn = 2;
    private bool _fullUsed;
    private bool _overchargeUsed;
    private bool _overchargeQuickUsed;
    private int _overchargeHeatStep;
    private bool _playerImpaired;
    private bool _playerExposed;
    private int _exposedTurnsRemaining;
    /// <summary>Nuclear Cavalier: first Danger Zone attack this turn already consumed.</summary>
    private bool _nuclearCavalierUsedThisTurn;
    /// <summary>Overwatch uses spent this round (Hyper-Reflex allows 2).</summary>
    private int _overwatchUsesThisRound;
    private bool _braceUsedThisRound;
    /// <summary>Core: one reaction per character activation (enemy unit turn).</summary>
    private bool _reactionUsedThisActivation;
    /// <summary>Unit currently executing AI steps; used to reset reaction budget per enemy.</summary>
    private int _executingAiUnitId = -1;
    /// <summary>Hexes reserved by planned AI moves in the current activation queue.</summary>
    private readonly HashSet<LancerGridCoord> _reservedAiMoveCells = new();
    /// <summary>After Brace: no reactions until end of your next turn.</summary>
    private bool _braceReactionLockout;
    /// <summary>Brace aftermath: next player turn is one-quick only (no move/full/free/OC).</summary>
    private bool _braceRestrictedPending;
    private bool _braceRestrictedTurn;
    /// <summary>While true, attacks against the player take +1 DIFF (Brace lasting effect).</summary>
    private bool _braceDefenseActive;
    /// <summary>Reactor meltdown pending at end of / start of next player turn.</summary>
    private bool _reactorMeltdownPending;

    private LancerSelectionMode _selectionMode;
    private LancerAttackIntent _attackIntent;
    private int _selectedWeaponIndex = -1;
    private readonly List<int> _barragePickedWeapons = new();
    private readonly Queue<int> _barrageWeaponQueue = new();
    private int _barrageCurrentTargetId = -1;
    private int _briefingStep;

    private bool _reactionPromptActive;
    private LancerReactionType _pendingReaction;
    private float _reactionTimer;
    private PendingReactionContext _reactionContext;
    private int _reactionUnitId = -1;
    /// <summary>Semper Vigilo enter-trigger: resolve Overwatch after the mover finishes entering threat.</summary>
    private bool _overwatchAfterMove;
    /// <summary>Hyper-Reflex: Immobilize the Overwatch target if the shot hits.</summary>
    private int _hyperReflexImmobilizeUnitId = -1;
    private int _pendingBraceDamage;
    private int _pendingBraceRoll;
    private int _pendingBraceAcc;
    private int _pendingBraceDiff;
    private bool _pendingBraceHit;
    private bool _pendingBraceCrit;
    private UnitRecord? _pendingBraceAttacker;
    private UnitRecord? _pendingBraceTarget;

    private float _aiStepTimer;
    private bool _processingAi;
    private PendingResolution? _pendingResolution;

    private readonly bool[] _weaponUsedThisTurn = new bool[WeaponSlotCount];
    private Action? _pendingReactionContinue;

    public bool Started => _phase != LancerGamePhase.Briefing || _combatStarted;
}

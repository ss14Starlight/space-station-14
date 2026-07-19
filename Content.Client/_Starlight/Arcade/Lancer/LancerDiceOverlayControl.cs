using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Arcade.Lancer;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Arcade.Lancer;

/// <summary>
/// Full-column overlay that throws dice with pseudo-physics on top of the Lancer board column.
/// Attack dice and damage dice are separate waves with a short delay between them.
/// </summary>
public sealed class LancerDiceOverlayControl : Control
{
    private const float D6Size = 64f;
    private const float D20Size = 96f;
    private const float Gravity = 1200f;
    private const float Restitution = 0.45f;
    private const float SettleSpeedThreshold = 80f;
    private const float MaxTumbleTime = 1.2f;
    private const float DamageDelay = 0.5f;
    private const float HoldDuration = 2f;
    private const float FadeDuration = 0.4f;
    private const float MinRollWidth = 160f;
    private const float MinRollHeight = 120f;

    private static readonly ResPath DiceRsi = new("Objects/Fun/dice.rsi");
    private static readonly Color AccTint = Color.FromHex("#88CC88");
    private static readonly Color DiffTint = Color.FromHex("#CC8888");
    private static readonly Color DamageTint = Color.FromHex("#E8A040");

    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Queue<LancerArcadeMessages.LancerDiceRollMessage> _queue = new();
    private ActiveRoll? _active;
    private RSIResource? _diceRsi;
    private LancerBoardControl? _boardControl;
    private Control? _dedicatedArea;
    private Control? _phasePanel;
    private Control? _columnRoot;
    private bool _preferBoard;
    private VectorFont? _titleFont;
    private VectorFont? _resultFont;
    private VectorFont? _d20FaceFont;

    private enum RollPhase
    {
        AttackWave,
        DamageDelay,
        DamageWave,
        Hold,
        FadeOut
    }

    private enum DieWave
    {
        Attack,
        Damage
    }

    private sealed class DieBody
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Rotation;
        public float AngularVel;
        public string Kind = "";
        public Color Tint = Color.White;
        public DieWave Wave;
        public int FinalFace;
        public int DisplayFace;
        public bool Settled;
        public float TumbleElapsed;
        public float NextFlip;
        public float Scale = 1f;
        public float SettlePop;
    }

    private sealed class ActiveRoll
    {
        public LancerArcadeMessages.LancerDiceRollMessage Message = default!;
        public RollPhase Phase = RollPhase.AttackWave;
        public float PhaseElapsed;
        public float OverallAlpha = 1f;
        public readonly List<DieBody> Dice = new();
        public bool ShowAttackResult;
        public bool ShowDamageResult;
        public bool NeedsAttackSpawn = true;
        public string AttackResultText = "";
        public string DamageResultText = "";
        public Color ResultColor;
    }

    public LancerDiceOverlayControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;
        VerticalExpand = true;
        AlwaysRender = true;

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf");
        _titleFont = new VectorFont(fontRes, 14);
        _resultFont = new VectorFont(fontRes, 16);
        _d20FaceFont = new VectorFont(fontRes, 36);
    }

    public void BindBoard(LancerBoardControl boardControl)
    {
        _boardControl = boardControl;
    }

    public void BindColumn(Control columnRoot)
    {
        _columnRoot = columnRoot;
    }

    public void SetRollContext(Control? dedicatedArea, Control? phasePanel, bool preferBoard)
    {
        _dedicatedArea = dedicatedArea;
        _phasePanel = phasePanel;
        _preferBoard = preferBoard;
    }

    public void Enqueue(LancerArcadeMessages.LancerDiceRollMessage msg)
    {
        _queue.Enqueue(msg);
        if (_active == null)
            StartNext();
    }

    public void Clear()
    {
        _queue.Clear();
        _active = null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (_active == null)
            return;

        if (_active.NeedsAttackSpawn)
        {
            SpawnAttackDice(_active);
            _active.NeedsAttackSpawn = false;
        }

        var dt = args.DeltaSeconds;
        _active.PhaseElapsed += dt;

        switch (_active.Phase)
        {
            case RollPhase.AttackWave:
                UpdateDicePhysics(_active, DieWave.Attack, dt);
                if (AllWaveSettled(_active, DieWave.Attack))
                {
                    _active.ShowAttackResult = true;
                    BuildAttackResult(_active);

                    if (ShouldShowDamageWave(_active.Message))
                    {
                        _active.Phase = RollPhase.DamageDelay;
                        _active.PhaseElapsed = 0;
                    }
                    else
                    {
                        _active.Phase = RollPhase.Hold;
                        _active.PhaseElapsed = 0;
                    }
                }

                break;

            case RollPhase.DamageDelay:
                ClampAllDice(_active, GetDiceBounds());
                if (_active.PhaseElapsed >= DamageDelay)
                {
                    SpawnDamageDice(_active);
                    _active.Phase = RollPhase.DamageWave;
                    _active.PhaseElapsed = 0;
                }

                break;

            case RollPhase.DamageWave:
                UpdateDicePhysics(_active, DieWave.Damage, dt);
                if (AllWaveSettled(_active, DieWave.Damage))
                {
                    _active.ShowDamageResult = true;
                    BuildDamageResult(_active);
                    _active.Phase = RollPhase.Hold;
                    _active.PhaseElapsed = 0;
                }

                break;

            case RollPhase.Hold:
                ClampAllDice(_active, GetDiceBounds());
                if (_active.PhaseElapsed >= HoldDuration)
                {
                    _active.Phase = RollPhase.FadeOut;
                    _active.PhaseElapsed = 0;
                }

                break;

            case RollPhase.FadeOut:
                _active.OverallAlpha = 1f - Math.Clamp(_active.PhaseElapsed / FadeDuration, 0f, 1f);
                if (_active.PhaseElapsed >= FadeDuration)
                {
                    _active = null;
                    if (_queue.Count > 0)
                        StartNext();
                }

                break;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_active == null || _titleFont == null || _resultFont == null || _d20FaceFont == null)
            return;

        var alpha = _active.OverallAlpha;
        if (alpha <= 0.001f)
            return;

        foreach (var die in _active.Dice)
        {
            var tex = TryGetDieTexture(die.Kind, die.Settled ? die.FinalFace : die.DisplayFace);
            if (tex == null)
                continue;

            var drawSize = GetDieDrawSize(die.Kind);
            var scale = die.Scale;
            if (die.Settled && die.SettlePop > 0)
                scale *= 1f + die.SettlePop * 0.15f;

            var half = new Vector2(drawSize, drawSize) * scale * 0.5f;
            var box = UIBox2.FromDimensions(die.Pos - half, half * 2f);
            handle.DrawTextureRect(tex, box, die.Tint.WithAlpha(alpha));

            if (die.Kind == "d20")
                DrawD20FaceNumber(handle, die, alpha);
        }

        var msg = _active.Message;
        var titleColor = msg.IsPlayerRoll
            ? Color.FromHex("#3A6AAA").WithAlpha(alpha)
            : Color.FromHex("#AA3A3A").WithAlpha(alpha);

        var bounds = GetDiceBounds();
        var titlePos = new Vector2(bounds.Center.X, bounds.Top + 8f * UIScale);
        var titleDims = handle.GetDimensions(_titleFont, msg.SourceLabel, 1f);
        handle.DrawString(_titleFont, titlePos - new Vector2(titleDims.X * 0.5f, 0), msg.SourceLabel, titleColor);

        if (_active.ShowAttackResult)
        {
            var resultPos = new Vector2(bounds.Center.X, bounds.Bottom - 56f * UIScale);
            var resultDims = handle.GetDimensions(_resultFont, _active.AttackResultText, 1f);
            handle.DrawString(
                _resultFont,
                resultPos - new Vector2(resultDims.X * 0.5f, 0),
                _active.AttackResultText,
                _active.ResultColor.WithAlpha(alpha));
        }

        if (_active.ShowDamageResult)
        {
            var damagePos = new Vector2(bounds.Center.X, bounds.Bottom - 28f * UIScale);
            var damageDims = handle.GetDimensions(_resultFont, _active.DamageResultText, 1f);
            handle.DrawString(
                _resultFont,
                damagePos - new Vector2(damageDims.X * 0.5f, 0),
                _active.DamageResultText,
                DamageTint.WithAlpha(alpha));
        }
    }

    private void DrawD20FaceNumber(DrawingHandleScreen handle, DieBody die, float alpha)
    {
        var faceText = (die.Settled ? die.FinalFace : die.DisplayFace).ToString();
        var dims = handle.GetDimensions(_d20FaceFont!, faceText, 1f);
        var pos = die.Pos - dims * 0.5f;
        var shadow = Color.FromHex("#111111").WithAlpha(alpha * 0.85f);
        var text = Color.White.WithAlpha(alpha);

        handle.DrawString(_d20FaceFont!, pos + new Vector2(1.5f, 1.5f), faceText, shadow);
        handle.DrawString(_d20FaceFont!, pos, faceText, text);
    }

    private void StartNext()
    {
        if (_queue.Count == 0)
            return;

        var msg = _queue.Dequeue();
        _active = new ActiveRoll { Message = msg };
    }

    private void SpawnAttackDice(ActiveRoll roll)
    {
        var msg = roll.Message;
        var bounds = GetDiceBounds();
        var spawn = GetSpawnPoint(msg.IsPlayerRoll, bounds);

        if (msg.D20 > 0)
            roll.Dice.Add(CreateDie("d20", msg.D20, Color.White, DieWave.Attack, spawn, bounds, msg.IsPlayerRoll));

        foreach (var face in msg.AccDice)
            roll.Dice.Add(CreateDie("d6", face, AccTint, DieWave.Attack, spawn, bounds, msg.IsPlayerRoll));

        foreach (var face in msg.DiffDice)
            roll.Dice.Add(CreateDie("d6", face, DiffTint, DieWave.Attack, spawn, bounds, msg.IsPlayerRoll));

        if (roll.Dice.Count == 0)
        {
            roll.ShowAttackResult = true;
            BuildAttackResult(roll);
            roll.Phase = roll.Message.Hit && roll.Message.DamageDice.Length > 0
                ? RollPhase.DamageDelay
                : RollPhase.Hold;
            roll.PhaseElapsed = 0;
        }
    }

    private void SpawnDamageDice(ActiveRoll roll)
    {
        var msg = roll.Message;
        var bounds = GetDiceBounds();
        var spawn = GetSpawnPoint(msg.IsPlayerRoll, bounds);

        foreach (var face in msg.DamageDice)
            roll.Dice.Add(CreateDie("d6", face, DamageTint, DieWave.Damage, spawn, bounds, msg.IsPlayerRoll));

        if (roll.Dice.All(d => d.Wave != DieWave.Damage))
        {
            roll.ShowDamageResult = true;
            BuildDamageResult(roll);
            roll.Phase = RollPhase.Hold;
            roll.PhaseElapsed = 0;
        }
    }

    private DieBody CreateDie(
        string kind,
        int finalFace,
        Color tint,
        DieWave wave,
        Vector2 spawn,
        UIBox2 bounds,
        bool isPlayerRoll)
    {
        var angle = _random.NextFloat() * MathF.PI * 2f;
        var spread = _random.NextFloat() * 48f;
        var pos = spawn + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spread;
        pos = ClampToBounds(pos, GetDieRadius(kind), bounds);

        var center = bounds.Center;
        var toCenter = center - pos;
        if (toCenter.LengthSquared() < 0.001f)
            toCenter = new Vector2(isPlayerRoll ? 1f : -1f, 0.5f);

        toCenter = Vector2.Normalize(toCenter);
        var speed = _random.NextFloat(220f, 420f);
        var vel = toCenter * speed + new Vector2(0f, _random.NextFloat(-80f, 120f));

        return new DieBody
        {
            Pos = pos,
            Vel = vel,
            Rotation = _random.NextFloat(0f, 360f),
            AngularVel = _random.NextFloat(-720f, 720f),
            Kind = kind,
            Tint = tint,
            Wave = wave,
            FinalFace = finalFace,
            DisplayFace = kind == "d20" ? _random.Next(1, 21) : _random.Next(1, 7),
            NextFlip = 0
        };
    }

    private UIBox2 GetDiceBounds()
    {
        var minSize = new Vector2(MinRollWidth, MinRollHeight) * UIScale;

        if (_preferBoard && TryGetBoardBounds(out var boardBounds))
            return NormalizeBounds(boardBounds, minSize);

        var candidates = new List<UIBox2>();

        if (TryGetRelativeBounds(_dedicatedArea, out var dedicatedBounds))
            candidates.Add(dedicatedBounds);

        if (TryGetRelativeBounds(_phasePanel, out var phaseBounds))
            candidates.Add(TrimBottom(phaseBounds, 72f * UIScale));

        if (TryGetRelativeBounds(_columnRoot, out var columnBounds))
            candidates.Add(TrimTop(columnBounds, 28f * UIScale));

        candidates.Add(GetOverlayFallbackBounds());

        UIBox2? best = null;
        var bestArea = 0f;

        foreach (var candidate in candidates)
        {
            var usable = NormalizeBounds(candidate, minSize);
            var area = usable.Width * usable.Height;
            if (area > bestArea)
            {
                bestArea = area;
                best = usable;
            }
        }

        return best ?? NormalizeBounds(GetOverlayFallbackBounds(), minSize);
    }

    private UIBox2 GetOverlayFallbackBounds()
    {
        var top = 28f * UIScale;
        var margin = 8f * UIScale;
        var minHeight = MinRollHeight * UIScale;
        return new UIBox2(
            margin,
            top + margin,
            MathF.Max(PixelSize.X - margin, margin + MinRollWidth * UIScale),
            MathF.Max(PixelSize.Y - margin, top + margin + minHeight));
    }

    private bool TryGetBoardBounds(out UIBox2 bounds)
    {
        bounds = default;

        if (_boardControl is not { VisibleInTree: true, IsInsideTree: true })
            return false;

        var playArea = _boardControl.GetPlayAreaPixelBounds();
        if (playArea.Width < 8f || playArea.Height < 8f)
            return false;

        var offset = _boardControl.GlobalPixelPosition - GlobalPixelPosition;
        bounds = new UIBox2(offset + playArea.TopLeft, offset + playArea.BottomRight);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private bool TryGetRelativeBounds(Control? control, out UIBox2 bounds)
    {
        bounds = default;

        if (control is not { VisibleInTree: true, IsInsideTree: true })
            return false;

        if (control.PixelSize.X < 4f || control.PixelSize.Y < 4f)
            return false;

        bounds = GetControlBounds(control);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private UIBox2 GetControlBounds(Control control)
    {
        var offset = control.GlobalPixelPosition - GlobalPixelPosition;
        return new UIBox2(offset, offset + control.PixelSize);
    }

    private static UIBox2 TrimTop(UIBox2 bounds, float amount)
    {
        if (amount <= 0 || bounds.Height <= amount + 8f)
            return bounds;

        return new UIBox2(bounds.Left, bounds.Top + amount, bounds.Right, bounds.Bottom);
    }

    private static UIBox2 TrimBottom(UIBox2 bounds, float amount)
    {
        if (amount <= 0 || bounds.Height <= amount + 8f)
            return bounds;

        return new UIBox2(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom - amount);
    }

    private UIBox2 NormalizeBounds(UIBox2 bounds, Vector2 minSize)
    {
        var width = MathF.Max(bounds.Width, minSize.X);
        var height = MathF.Max(bounds.Height, minSize.Y);

        if (width > bounds.Width || height > bounds.Height)
        {
            var center = bounds.Center;
            bounds = UIBox2.FromDimensions(
                center - new Vector2(width * 0.5f, height * 0.5f),
                new Vector2(width, height));
        }

        var maxInset = MathF.Min(
            GetDieRadius("d20") + 2f,
            MathF.Min(bounds.Width, bounds.Height) * 0.15f);

        var insetted = InsetBounds(bounds, maxInset);
        if (insetted.Width < minSize.X * 0.5f || insetted.Height < minSize.Y * 0.5f)
            return bounds;

        return insetted;
    }

    private static UIBox2 InsetBounds(UIBox2 bounds, float inset)
    {
        if (inset <= 0)
            return bounds;

        return new UIBox2(
            bounds.Left + inset,
            bounds.Top + inset,
            bounds.Right - inset,
            bounds.Bottom - inset);
    }

    private Vector2 GetSpawnPoint(bool isPlayerRoll, UIBox2 bounds)
    {
        var radius = GetDieRadius("d20");
        var y = bounds.Top + radius + 4f;
        var x = isPlayerRoll
            ? bounds.Left + radius + 4f
            : bounds.Right - radius - 4f;
        return new Vector2(x, y);
    }

    private void UpdateDicePhysics(ActiveRoll roll, DieWave wave, float dt)
    {
        var bounds = GetDiceBounds();
        var waveDice = roll.Dice.Where(d => d.Wave == wave).ToList();

        foreach (var die in waveDice)
        {
            if (!die.Settled)
            {
                die.Vel.Y += Gravity * dt;
                die.Pos += die.Vel * dt;
                die.Rotation += die.AngularVel * dt;

                var radius = GetEffectiveRadius(die);
                BounceWalls(die, bounds, radius);
                die.Pos = ClampToBounds(die.Pos, radius, bounds);

                die.TumbleElapsed += dt;
                if (die.TumbleElapsed >= die.NextFlip)
                {
                    die.DisplayFace = die.Kind == "d20" ? _random.Next(1, 21) : _random.Next(1, 7);
                    var progress = Math.Clamp(die.TumbleElapsed / MaxTumbleTime, 0f, 1f);
                    die.NextFlip = die.TumbleElapsed + MathHelper.Lerp(0.04f, 0.18f, progress);
                }

                if (die.Vel.Length() < SettleSpeedThreshold || die.TumbleElapsed >= MaxTumbleTime)
                    SettleDie(die);
            }
            else if (die.SettlePop > 0)
            {
                die.SettlePop = MathF.Max(0f, die.SettlePop - dt * 4f);
                die.Rotation = MathHelper.Lerp(die.Rotation, 0f, dt * 12f);
            }
        }

        SeparateDice(roll.Dice, bounds);
        ClampAllDice(roll, bounds);
    }

    private static void BounceWalls(DieBody die, UIBox2 bounds, float radius)
    {
        if (die.Pos.X - radius < bounds.Left)
        {
            die.Pos.X = bounds.Left + radius;
            die.Vel.X = MathF.Abs(die.Vel.X) * Restitution;
            die.AngularVel *= 0.7f;
        }

        if (die.Pos.X + radius > bounds.Right)
        {
            die.Pos.X = bounds.Right - radius;
            die.Vel.X = -MathF.Abs(die.Vel.X) * Restitution;
            die.AngularVel *= 0.7f;
        }

        if (die.Pos.Y - radius < bounds.Top)
        {
            die.Pos.Y = bounds.Top + radius;
            die.Vel.Y = MathF.Abs(die.Vel.Y) * Restitution;
            die.AngularVel *= 0.7f;
        }

        if (die.Pos.Y + radius > bounds.Bottom)
        {
            die.Pos.Y = bounds.Bottom - radius;
            die.Vel.Y = -MathF.Abs(die.Vel.Y) * Restitution;
            die.AngularVel *= 0.7f;
            die.Vel.X *= 0.92f;
        }
    }

    private void ClampAllDice(ActiveRoll roll, UIBox2 bounds)
    {
        foreach (var die in roll.Dice)
        {
            var radius = GetEffectiveRadius(die);
            die.Pos = ClampToBounds(die.Pos, radius, bounds);
        }
    }

    private float GetEffectiveRadius(DieBody die)
    {
        var scale = die.Settled && die.SettlePop > 0 ? 1f + die.SettlePop * 0.15f : die.Scale;
        return GetDieRadius(die.Kind) * scale;
    }

    private static Vector2 ClampToBounds(Vector2 pos, float radius, UIBox2 bounds)
    {
        var minX = bounds.Left + radius;
        var maxX = bounds.Right - radius;
        var minY = bounds.Top + radius;
        var maxY = bounds.Bottom - radius;

        if (maxX < minX || maxY < minY)
            return bounds.Center;

        return new Vector2(
            Math.Clamp(pos.X, minX, maxX),
            Math.Clamp(pos.Y, minY, maxY));
    }

    private void SeparateDice(List<DieBody> dice, UIBox2 bounds)
    {
        for (var pass = 0; pass < 4; pass++)
        {
            for (var i = 0; i < dice.Count; i++)
            {
                for (var j = i + 1; j < dice.Count; j++)
                {
                    var a = dice[i];
                    var b = dice[j];
                    var delta = b.Pos - a.Pos;
                    var dist = delta.Length();
                    var minDist = GetEffectiveRadius(a) + GetEffectiveRadius(b);
                    if (dist >= minDist || dist < 0.001f)
                        continue;

                    var push = delta / dist * (minDist - dist);
                    if (!a.Settled && !b.Settled)
                    {
                        a.Pos -= push * 0.5f;
                        b.Pos += push * 0.5f;
                    }
                    else if (!a.Settled)
                    {
                        a.Pos -= push;
                    }
                    else if (!b.Settled)
                    {
                        b.Pos += push;
                    }
                }
            }

            foreach (var die in dice)
            {
                var radius = GetEffectiveRadius(die);
                die.Pos = ClampToBounds(die.Pos, radius, bounds);
            }
        }
    }

    private void SettleDie(DieBody die)
    {
        die.Settled = true;
        die.DisplayFace = die.FinalFace;
        die.Vel = Vector2.Zero;
        die.AngularVel = 0;
        die.SettlePop = 1f;
    }

    private static bool AllWaveSettled(ActiveRoll roll, DieWave wave)
    {
        var waveDice = roll.Dice.Where(d => d.Wave == wave).ToList();
        return waveDice.Count == 0 || waveDice.All(d => d.Settled);
    }

    private static bool ShouldShowDamageWave(LancerArcadeMessages.LancerDiceRollMessage msg)
    {
        if (msg.DamageDice.Length == 0)
            return false;

        // Saves always deal damage (full or half); attacks only on hit.
        return msg.Kind switch
        {
            LancerRollKind.Save => true,
            LancerRollKind.Attack => msg.Hit,
            _ => false
        };
    }

    private void BuildAttackResult(ActiveRoll roll)
    {
        var msg = roll.Message;

        switch (msg.Kind)
        {
            case LancerRollKind.Spot:
            {
                var result = msg.Hit
                    ? Loc.GetString("lancer-arcade-dice-spot-pass")
                    : Loc.GetString("lancer-arcade-dice-spot-fail");

                var spotVs = msg.TargetNumber > 0
                    ? Loc.GetString("lancer-arcade-dice-vs", ("total", msg.Total), ("target", msg.TargetNumber))
                    : msg.Total.ToString();

                roll.AttackResultText = $"{spotVs}  {result}";
                roll.ResultColor = msg.Hit
                    ? Color.FromHex("#88EE88")
                    : Color.FromHex("#EE8888");
                return;
            }
            case LancerRollKind.Save:
            {
                // Hit means failed save (full damage); miss means passed (half damage).
                var result = msg.Hit
                    ? Loc.GetString("lancer-arcade-dice-save-fail")
                    : Loc.GetString("lancer-arcade-dice-save-pass");

                var saveVs = msg.TargetNumber > 0
                    ? Loc.GetString("lancer-arcade-dice-vs", ("total", msg.Total), ("target", msg.TargetNumber))
                    : msg.Total.ToString();

                roll.AttackResultText = $"{saveVs}  {result}";
                roll.ResultColor = msg.Hit
                    ? Color.FromHex("#EE8888")
                    : Color.FromHex("#88EE88");
                return;
            }
            case LancerRollKind.StructureCheck:
                roll.AttackResultText = Loc.GetString("lancer-arcade-dice-structure", ("roll", msg.Total));
                roll.ResultColor = Color.FromHex("#E8C84A");
                return;
            case LancerRollKind.OverheatCheck:
                roll.AttackResultText = Loc.GetString("lancer-arcade-dice-overheat", ("roll", msg.Total));
                roll.ResultColor = Color.FromHex("#E8C84A");
                return;
        }

        var attackResult = msg.Crit
            ? Loc.GetString("lancer-arcade-dice-crit")
            : msg.Hit
                ? Loc.GetString("lancer-arcade-dice-hit")
                : Loc.GetString("lancer-arcade-dice-miss");

        var vs = msg.TargetNumber > 0
            ? Loc.GetString("lancer-arcade-dice-vs", ("total", msg.Total), ("target", msg.TargetNumber))
            : msg.Total.ToString();

        roll.AttackResultText = $"{vs}  {attackResult}";
        roll.ResultColor = msg.Crit
            ? Color.FromHex("#E8C84A")
            : msg.Hit
                ? Color.FromHex("#88EE88")
                : Color.FromHex("#EE8888");
    }

    private static void BuildDamageResult(ActiveRoll roll)
    {
        var dice = roll.Message.DamageDice;
        if (dice.Length == 0)
        {
            roll.DamageResultText = string.Empty;
            return;
        }

        var sum = dice.Sum();
        roll.DamageResultText = dice.Length == 1
            ? $"Damage: {sum}"
            : $"Damage: {string.Join(" + ", dice)} = {sum}";
    }

    private float GetDieDrawSize(string kind) => (kind == "d20" ? D20Size : D6Size) * UIScale;

    private float GetDieRadius(string kind) => GetDieDrawSize(kind) * 0.5f;

    private Texture? TryGetDieTexture(string prefix, int value)
    {
        if (_diceRsi == null)
            _resourceCache.TryGetResource(SpriteSpecifierSerializer.TextureRoot / DiceRsi, out _diceRsi);

        if (_diceRsi == null || !_diceRsi.RSI.TryGetState($"{prefix}_{value}", out var state))
            return null;

        return state.GetFrame(RsiDirection.South, 0);
    }
}

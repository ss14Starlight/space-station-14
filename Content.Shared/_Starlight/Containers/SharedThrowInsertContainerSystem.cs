using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Administration.Logs;
using Content.Shared.Containers;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Containers;

public sealed partial class SharedThrowInsertContainerSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThrowInsertContainerComponent, ThrowHitByEvent>(OnThrowCollide);
    }

    private void OnThrowCollide(Entity<ThrowInsertContainerComponent> ent, ref ThrowHitByEvent args)
    {
        var container = _containerSystem.GetContainer(ent, ent.Comp.ContainerId);

        if (!_containerSystem.CanInsert(args.Thrown, container))
            return;

        var beforeThrowArgs = new BeforeThrowInsertEvent(args.Thrown);
        RaiseLocalEvent(ent, ref beforeThrowArgs);

        if (beforeThrowArgs.Cancelled)
            return;

        if (!_random.ProbPredicted(_timing, ent.Comp.Probability))
        {
            _audio.PlayPredicted(ent.Comp.MissSound, ent, args.Component.Thrower);
            _popup.PopupPredicted(Loc.GetString(ent.Comp.MissLocString), ent, args.Component.Thrower);
            return;
        }

        if (!_containerSystem.Insert(args.Thrown, container))
            throw new InvalidOperationException("Container insertion failed but CanInsert returned true");

        _audio.PlayPredicted(ent.Comp.InsertSound, ent, args.Component.Thrower);

        if (args.Component.Thrower != null)
            _adminLogger.Add(LogType.Landed, LogImpact.Low, $"{ToPrettyString(args.Thrown)} thrown by {ToPrettyString(args.Component.Thrower.Value):player} landed in {ToPrettyString(ent)}");
    }
}

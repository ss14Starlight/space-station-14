using System.Linq;
using Content.Shared._Starlight.Admeme.Jumpscare;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Admeme.Jumpscare;

public sealed partial class JumpscareSystem : SharedJumpscareSystem
{
    [Dependency] private SharedAudioSystem _audio = null!;

    private readonly HashSet<JumpscarePanelEui> _openPanels = [];

    public void Jumpscare(ICommonSession session, JumpscareData jumpscare)
    {
        _audio.PlayGlobal(jumpscare.Sound, session);
        RaiseNetworkEvent(new DoJumpscareEvent(jumpscare, true), session);
    }

    public void Jumpscare(EntityUid uid, JumpscareData jumpscare)
    {
        var comp = EnsureComp<JumpscaredComponent>(uid);
        comp.Jumpscare = jumpscare;
        Dirty(uid, comp);
    }

    protected override void Jumpscare(Entity<JumpscaredComponent> ent)
    {
        TryComp<ActorComponent>(ent, out var actor);
        if (ent.Comp.Jumpscare.SoundAudibleToOthers)
            _audio.PlayPvs(ent.Comp.Jumpscare.Sound, ent);
        else if (actor != null)
            _audio.PlayGlobal(ent.Comp.Jumpscare.Sound, actor.PlayerSession);

        base.Jumpscare(ent);
        if (actor != null)
            RaiseNetworkEvent(new DoJumpscareEvent(ent.Comp.Jumpscare, true), actor.PlayerSession);
    }

    public void AddPanel(JumpscarePanelEui panel) => _openPanels.Add(panel);

    public void RemovePanel(JumpscarePanelEui panel) => _openPanels.Remove(panel);

    [SubscribeLocalEvent]
    private void OnTerminating(ref EntityTerminatingEvent args)
    {
        if (_openPanels.Count == 0)
            return;

        var uid = args.Entity.Owner;
        foreach (var panel in _openPanels.ToArray())
            if (panel.LockedTarget == uid)
                panel.Close();
    }
}

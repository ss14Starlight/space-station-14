using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Administration.Components;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class KillSignCommand : ToolshedCommand
{
    private static readonly string BaseContentPath = "Objects/Misc/killsign.rsi";
    private static readonly string SLContentPath = "_Starlight/Objects/Misc/killsign.rsi";

    [CommandImplementation("kill")]
    public EntityUid Kill([PipedArgument] EntityUid uid)
    {
        EnsureComp<KillSignComponent>(uid);
        return uid;
    }

    [CommandImplementation("stinky")]
    public EntityUid Stinky([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "stinky");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("raider")]
    public EntityUid Raider([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "raider");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("peak")]
    public EntityUid Peak([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "peak");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("nerd")]
    public EntityUid Nerd([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "nerd");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("it")]
    public EntityUid It([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "it");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("furry")]
    public EntityUid Furry([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "furry");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("dog")]
    public EntityUid Dog([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "dog");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("cat")]
    public EntityUid Cat([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "cat");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("bald")]
    public EntityUid Bald([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(BaseContentPath), "bald");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("w")]
    public EntityUid W([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "w");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("vip")]
    public EntityUid Vip([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "vip");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("ssd")]
    public EntityUid Ssd([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "ssd");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("uwu")]
    public EntityUid Uwu([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "uwu");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("owo")]
    public EntityUid Owo([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "owo");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("moff")]
    public EntityUid Moff([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "moff");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("l")]
    public EntityUid L([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "l");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("honk")]
    public EntityUid Honk([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "honk");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("harmbatong")]
    public EntityUid HarmBatong([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "harmbatong");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("gay")]
    public EntityUid Gay([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "gay");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("fat")]
    public EntityUid Fat([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "fat");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("event")]
    public EntityUid Event([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "event");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("dumb")]
    public EntityUid Dumb([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "dumb");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("dm")]
    public EntityUid Dm([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "dm");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("clueless")]
    public EntityUid Clueless([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "clueless");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("blind")]
    public EntityUid Blind([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "blind");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("admin")]
    public EntityUid Admin([PipedArgument] EntityUid uid)
    {
        var comp = EnsureComp<KillSignComponent>(uid);
        comp.Sprite = new SpriteSpecifier.Rsi(new ResPath(SLContentPath), "admin");
        EntityManager.Dirty(uid, comp);
        return uid;
    }

    [CommandImplementation("rm")]
    public EntityUid RemoveKillSign([PipedArgument] EntityUid uid)
    {
        RemComp<KillSignComponent>(uid);
        return uid;
    }

    [CommandImplementation("kill")]
    public IEnumerable<EntityUid> Kill([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Kill);

    [CommandImplementation("stinky")]
    public IEnumerable<EntityUid> Stinky([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Stinky);

    [CommandImplementation("raider")]
    public IEnumerable<EntityUid> Raider([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Raider);

    [CommandImplementation("peak")]
    public IEnumerable<EntityUid> Peak([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Peak);

    [CommandImplementation("nerd")]
    public IEnumerable<EntityUid> Nerd([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Nerd);

    [CommandImplementation("it")]
    public IEnumerable<EntityUid> It([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(It);

    [CommandImplementation("furry")]
    public IEnumerable<EntityUid> Furry([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Furry);

    [CommandImplementation("dog")]
    public IEnumerable<EntityUid> Dog([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Dog);

    [CommandImplementation("cat")]
    public IEnumerable<EntityUid> Cat([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Cat);

    [CommandImplementation("bald")]
    public IEnumerable<EntityUid> Bald([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Bald);

    [CommandImplementation("w")]
    public IEnumerable<EntityUid> W([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(W);

    [CommandImplementation("vip")]
    public IEnumerable<EntityUid> Vip([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Vip);

    [CommandImplementation("ssd")]
    public IEnumerable<EntityUid> Ssd([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Ssd);

    [CommandImplementation("uwu")]
    public IEnumerable<EntityUid> Uwu([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Uwu);

    [CommandImplementation("owo")]
    public IEnumerable<EntityUid> Owo([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Owo);

    [CommandImplementation("moff")]
    public IEnumerable<EntityUid> Moff([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Moff);

    [CommandImplementation("l")]
    public IEnumerable<EntityUid> L([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(L);

    [CommandImplementation("honk")]
    public IEnumerable<EntityUid> Honk([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Honk);

    [CommandImplementation("harmbatong")]
    public IEnumerable<EntityUid> HarmBatong([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(HarmBatong);

    [CommandImplementation("gay")]
    public IEnumerable<EntityUid> Gay([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Gay);

    [CommandImplementation("fat")]
    public IEnumerable<EntityUid> Fat([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Fat);

    [CommandImplementation("event")]
    public IEnumerable<EntityUid> Event([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Event);

    [CommandImplementation("dumb")]
    public IEnumerable<EntityUid> Dumb([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Dumb);

    [CommandImplementation("dm")]
    public IEnumerable<EntityUid> Dm([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Dm);

    [CommandImplementation("clueless")]
    public IEnumerable<EntityUid> Clueless([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Clueless);

    [CommandImplementation("blind")]
    public IEnumerable<EntityUid> Blind([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Blind);

    [CommandImplementation("admin")]
    public IEnumerable<EntityUid> Admin([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Admin);

    [CommandImplementation("rm")]
    public IEnumerable<EntityUid> RemoveKillSign([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(RemoveKillSign);
}

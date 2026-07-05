using Content.Shared.Paper;
using Content.Shared.Verbs;
using NuLua;
using NuLua.Luau;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Starlight.NuLua;

public sealed partial class LuaSystem : EntitySystem
{
    private LuauState? _state;

    [Dependency] private TransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        _state = LuauState.Create();
        _state.OpenLibraries();
        RegisterFunctions();

        SubscribeLocalEvent<LuauScriptComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerb);
    }

    private void OnGetAlternativeVerb(Entity<LuauScriptComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<PaperComponent>(ent, out var paper))
            return;
        if (_state is null)
            return;
        var executor = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = "run as luau script",
            Act = () =>
            {
                Log.Info($"Running Luau script using {ToPrettyString(ent)} paper component.");
                var thread = _state.CreateSandboxThread();
                RegisterThreadFunctions(thread, executor);
                var text = paper.Content;
                foreach (var line in text.Split('\n'))
                {
                    var results = thread.DoString(line);
                    Log.Info($"{ToPrettyString(ent)}: {(results.Length > 0 ? results[0] : "[no result]")}");
                }
            },
            Category = VerbCategory.Debug,
        });
    }

    private void RegisterFunctions()
    {
        if (_state is null) return;
        _state.RegisterFunction("tpe", (state, args) =>
        {
            var res = state["__executor"].Read<float>();
            // Log.Info($"result: {res}, length: {res.Length}");
            // Log.Info($"{res[0].Read<float>()}");
            // var executor = new EntityUid((int)state.DoString("GetEUID()")[0].Read<float>());
            var x = args[0].Read<float>();
            var y = args[1].Read<float>();
            var executor = new EntityUid((int)res);
            _xform.SetMapCoordinates(executor, new MapCoordinates(x, y, Transform(executor).MapID));
            return 0;
        });

    }

    private void RegisterThreadFunctions(LuauState thread, EntityUid executor)
    {
        thread["__executor"] = executor.Id;
        thread.RegisterFunction("GetEUID", (state, args) =>
        {
            state.Push(executor.Id);
            return 0;
        });
    }
}

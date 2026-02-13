using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Content.Server._Starlight.UXN.Devices;
using Content.Server._Starlight.UXN.Devices.ComponentDevices;
using Content.Server.Administration.Managers;
using Content.Shared._Starlight.UXN;
using Content.Shared.Administration.Managers;
using Content.Shared.Anomaly.Components;
using Content.Shared.Examine;
using Content.Shared.Fax.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Starlight.CCVar;
using Content.Shared.Verbs;
using Robust.Server.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.UXN;

public sealed partial class UxnSystem : SharedUxnSystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly ISharedAdminManager _adminManager = default!;
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;

    private readonly ResPath _compilerRom = new("/_Starlight/Uxn/Rom/drifloon.rom");

    private readonly UXNProcessor _compiler = new();

    private int _maxInstrs = 100000;
    private int _defaultInstrs = 1000;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UxnComponent, AfterInteractEvent>(OnInteractUsing);
        SubscribeLocalEvent<UxnAttachedComponent, ExaminedEvent>(OnExaminedAttached);
        SubscribeLocalEvent<UxnAttachedComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbAttached);

        #region Device subscriptions
        SubscribeLocalEvent<FaxMachineComponent, OnGetUxnDevices>(OnGetUxnDevicesFaxMachine);
        #endregion
        #region cvar subs
        _configurationManager.OnValueChanged(StarlightCCVars.UxnMaxInstrLimit, v => _maxInstrs = v);
        _configurationManager.OnValueChanged(StarlightCCVars.UxnDefaultInstrLimit, v => _defaultInstrs = v);
        #endregion
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var enumerator = EntityQueryEnumerator<UxnAttachedComponent>();
        var comps = new List<UxnAttachedComponent>();
        while (enumerator.MoveNext(out var comp1))
        {
            comps.Add(comp1);
        }
        var instrs = Math.Min(_defaultInstrs * (_maxInstrs / (_defaultInstrs * Math.Max(comps.Count,1))), _defaultInstrs);
        foreach (var item in comps)
        {
            item.Uxn?.RunLimited(instrs);
        }
    }

    private void OnInteractUsing(Entity<UxnComponent> ent, ref AfterInteractEvent ev)
    {
        if (ev.Target == null)
            return; //we cant interact with air.
        var target = ev.Target.Value;
        if (!(HasComp<UxnAttachableComponent>(target) || HasComp<PaperComponent>(target)))
            return; //the target is not a paper we can load code from. or something we can attach the UXN to.

        if (TryComp<PaperComponent>(target, out var paper))
        {
            if (Compile(paper.Content, out var error, out var rom))
            {
                ent.Comp.AssembledSize = rom.Count;
                ent.Comp.CompiledRom = rom;
                ent.Comp.CompilerOutput = error;
            } else
            {
                ent.Comp.AssembledSize = -1;
                ent.Comp.CompiledRom = new();
                ent.Comp.CompilerOutput = error;
            }
            Dirty(ent);
        }
        
        if (TryComp<UxnAttachableComponent>(target, out var attachable))
        {
            if (HasComp<UxnAttachedComponent>(target))
                return; //the target allready has a UXN attached. TODO: allow connecting mutiple UXNs to one machine and allowing them to mesh network.

            var attached = EnsureComp<UxnAttachedComponent>(target);
            var xform = Transform(ent);

            ContainerSlot cont = _containerSystem.HasContainer(target, attachable.UxnContainerId, null) ?
                (_containerSystem.GetContainer(target, attachable.UxnContainerId) as ContainerSlot)! :
                _containerSystem.MakeContainer<ContainerSlot>(target, attachable.UxnContainerId);
            attached.ChipHolder = cont;
            if (!_containerSystem.CanInsert(ent.Owner, attached.ChipHolder))
            {
                RemComp<UxnAttachedComponent>(target);
                return; //couldn't insert the uxn into a little slot on the machine so rem the attached comp and continue on.
            }

            var devEv = new OnGetUxnDevices();
            RaiseLocalEvent(target, ref devEv);

            var uxn = new UXNProcessor();
            uxn.SystemDevice.AttachableDevices = devEv.Devices;
            var mem = uxn.SystemMem;
            ushort writeHead = 0x100;
            Span<byte> span = new byte[32];
            var stream = new MemoryStream([.. ent.Comp.CompiledRom]);
            while (stream.CanRead)
            {
                var amnt = stream.Read(span);
                if (amnt == 0) break;
                for (int i = 0; i < amnt; i++)
                {
                    mem[writeHead] = span[i];
                    writeHead++;
                }
            }

            attached.Uxn = uxn;
            if (!_containerSystem.Insert((ent.Owner, xform), cont))
                RemComp<UxnAttachedComponent>(target); //Failed to actually INSERT the chip into the machine... cri
        }
    }

    private void OnExaminedAttached(Entity<UxnAttachedComponent> ent, ref ExaminedEvent ev)
    {
        var uxn = ent.Comp.Uxn;
        if (uxn == null)
            return; //somehow we have an attached component but no uxn. shouldn't be possible but just in case.
        ev.PushMarkup(Loc.GetString("uxn-attached-examine", [("running", uxn.Running), ("instrs", uxn.RealInstructionCounter)]));
    }

    private void OnGetInteractionVerbAttached(Entity<UxnAttachedComponent> ent, ref GetVerbsEvent<InteractionVerb> ev)
    {
        if (!ev.CanAccess)
            return;

        var user = ev.User;
        ev.Verbs.Add(new()
        {
            Act = () =>
            {
                _handsSystem.PickupOrDrop(user, ent.Comp.ChipHolder.ContainedEntity!.Value);
                RemComp<UxnAttachedComponent>(ent);
            },
            Text = Loc.GetString("uxn-attached-take")
        });
    }

    private void OnGetUxnDevicesFaxMachine(Entity<FaxMachineComponent> ent, ref OnGetUxnDevices ev)
    {
        ComponentUxnDevice<FaxMachineComponent> dev = new FaxComponentDevice();
        ev.AddDevice(ent, dev);
    }

    public bool Compile(string uxnTal, out string error, [NotNullWhen(true)] out List<byte>? rom)
    {
        if (!_resourceManager.ContentFileExists(_compilerRom))
        {
            error = $"Failed to load UXN assembler {_compilerRom}";
            rom = null;
            return false;
        }
        _compiler.Reset();
        var mem = _compiler.SystemMem;
        ushort writeHead = 0x100;
        var stream = _resourceManager.ContentFileRead(_compilerRom);
        Span<byte> span = new byte[32];
        while (stream.CanRead)
        {
            var amnt = stream.Read(span);
            if (amnt == 0) break;
            for (int i = 0; i < amnt; i++)
            {
                mem[writeHead] = span[i];
                writeHead++;
            }
        }

        var stdio = _compiler.AttachDevice(0x01, new FakeStdioDevice(uxnTal));

        // we allow it to run "unlimited" cause we know that the input uxntal has a finite length
        // the finite input is usually 10k chars max on paper
        // but it *should* run out of memory before assembling uxntal that big tbh.
        _compiler.RunUnlimited();

        Log.Info($"Assembled UXN program in {_compiler.RealInstructionCounter} instructions, FakedStdio provided {stdio.CharCount}/{uxnTal.Length} chars");

        var stdErr = stdio.FakedError;
        error = new string(Encoding.ASCII.GetChars([.. stdErr]));
        if (_compiler.SystemDevice.Status < 0x80)
        {
            Log.Error($"Failed to compile uxntal. drifloon error output:\n {error}");
            rom = null;
            return false;
        }

        rom = stdio.FakedOutput;
        return true;
    }
}

[ByRefEvent]
public struct OnGetUxnDevices
{
    public readonly Dictionary<string, UXNDevice> Devices = new();

    public void AddDevice<T>(Entity<T> ent, ComponentUxnDevice<T> dev) where T : IComponent
        => AddDevice(ent.Comp, dev, ent.Owner);
    public void AddDevice<T>(T comp, ComponentUxnDevice<T> dev, EntityUid ent) where T : IComponent
    {
        var typeName = comp.GetType().Name;
        Devices[typeName[..^"Component".Length].ToLower()] = dev;
        dev.Setup(ent, comp);
    }
    public void AddDevice(string name, UXNDevice dev)
        => this.Devices[name.ToLower()] = dev;

    public OnGetUxnDevices() { }
}
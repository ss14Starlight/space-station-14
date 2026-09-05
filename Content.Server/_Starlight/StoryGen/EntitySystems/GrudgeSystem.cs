
using Content.Shared.Humanoid;
using Content.Shared.GameTicking;
using Robust.Shared.Random;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.Item;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared._Starlight.StoryGen;

namespace Content.Server._Starlight.StoryGen;
public sealed partial class GrudgeSystem : EntitySystem
{
    // [Dependency] private StoryGenLocalizationManager _sglm = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private InventorySystem _inv = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;

    // DEBUG - REMOVE
    [Dependency] private ILogManager _logManager = default!;
    public const string SawmillId = "lazy.debugging";
    private ISawmill _sawmill = default!;
    // END DEBUG - REMOVE

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill(SawmillId);

        SubscribeLocalEvent<GrudgeBookComponent, ComponentInit>(OnGrudgeBookInit);
        SubscribeLocalEvent<GrudgeBookComponent, ComponentStartup>(OnGrudgeBookSpawn);
        SubscribeLocalEvent<GrudgeBookComponent, ComponentShutdown>(OnGrudgeBookShutdown);
        SubscribeLocalEvent<GrudgeBearerComponent, ComponentStartup>(OnGrudgeBearerSpawn);
        SubscribeLocalEvent<GrudgeBearerComponent, ComponentShutdown>(OnGrudgeBearerShutdown);

        SubscribeLocalEvent<GrudgeBookComponent, GrudgeBindEvent>(BindBookByEvent);
        SubscribeLocalEvent<GrudgeBearerComponent, PlayerSpawnCompleteEvent>(OnNewSubjectEncountered);
    }
    private void OnNewSubjectEncountered(Entity<GrudgeBearerComponent> entity, ref PlayerSpawnCompleteEvent ev)
    {
        if (HasComp<HumanoidAppearanceComponent>(ev.Mob)) {
            // todo: check that the bearer is actually alive
            if(GenerateGrudge(entity.Owner, ev.Mob)) {
                // play update sound
                _audioSystem.PlayPredicted(entity.Comp.GrudgeSound, entity.Owner, entity.Owner);
            }
        }
    }

    public bool GenerateGrudge(EntityUid bearer, EntityUid target)
    {
        _sawmill.Debug($"{bearer} is considering a grudge against {target}...");

        if (!TryComp<GrudgeBearerComponent>(bearer, out var bearerComp))
        {
            _sawmill.Debug($"{bearer} has no GrudgeBearerComponent.");
            return false; // possible due to e.g. polymorph
        }

        if(bearerComp.Book == null)
        {
            _sawmill.Debug($"{bearer} has no assigned GrudgeBook.");
            return false;
        }

        if (!TryComp<GrudgeBookComponent>(bearerComp.Book, out var bookComp))
        {
            _sawmill.Debug($"{bearer}'s 'book' isn't a GrudgeBook.");
            return false; // possible due to e.g. crazy admin VVwriting stuff carelessly
        }

        if (_random.NextFloat() > bearerComp.judginess)
        {
            _sawmill.Debug($"{bearer} wasn't feeling particularly grudgy... for now.");
            return false;
        }
        else
        {
            // finally, we can add a grudge

            if (!TryComp<PaperComponent>(bearerComp.Book, out var paperComp))
            {
                _sawmill.Debug($"GrudgeBook {bearerComp.Book}'s PaperComponent is missing.");
                return false; // do this redundant check also, just in case an admin goes nuts with VV
            }

            var grudges_to_generate = bookComp.multiGrudge;
            while (grudges_to_generate-- > 0) {
                var dataset = _protoMan.Index(bookComp.dataset);
                var template = _random.Pick(dataset.Values);

                var relativeDataset = _protoMan.Index(bookComp.relativeDataset);
                var relativeOwner = Loc.GetString(_random.Pick(relativeDataset.Values));
                var relativeTarget = Loc.GetString(_random.Pick(relativeDataset.Values));

                var depth = _random.NextByte() % bookComp.relativeDepth; // maximum number of generations to go back for grudging
                while(depth-- > 0)
                {
                    relativeOwner = relativeOwner + "'s " + Loc.GetString(_random.Pick(relativeDataset.Values));
                    relativeTarget = relativeTarget + "'s " + Loc.GetString(_random.Pick(relativeDataset.Values));
                }

                paperComp.Content = paperComp.Content + "\n\n" + Loc.GetString(template,
                    ("owner", bearer),
                    ("ownerAncestor", relativeOwner),
                    ("target", target),
                    ("targetAncestor", relativeTarget));

                TryDirty(bearerComp.Book.Value);

                _sawmill.Debug($"{bearer} has a new grudge against {target}!");
            }

            return true;
        }
    }

    private void OnGrudgeBookInit(Entity<GrudgeBookComponent> book, ref ComponentInit ev)
    {
        if (!TryComp<PaperComponent>(book, out var paperComp))
            throw new Exception("GrudgeBook defined without Paper!");

        paperComp.Content = "initialized";
        TryDirty(book);
    }

    private void OnGrudgeBookSpawn(Entity<GrudgeBookComponent> book, ref ComponentStartup ev)
    {
        PaperComponent paperComp = _entManager.GetComponent<PaperComponent>(book);

        var parent = Transform(book).ParentUid;

        _sawmill.Debug($"GrudgeBook {book} spawned; parent is {parent}.");

        if (HasComp<GrudgeBearerComponent>(parent)) {
            _sawmill.Debug($"Parent {parent} of book {book} has GrudgeBearerComponent; attempting binding.");
            if (BindBook(book, parent))
                return;
        }

        // in case the book gets dumped on the ground (untested)
        var enumerator = _entManager.AllEntityQueryEnumerator<GrudgeBearerComponent>();
        while (enumerator.MoveNext(out var uid, out _))
        {
            if (Transform(uid).Coordinates == Transform(book).Coordinates
            && Transform(uid).ParentUid == parent)
            {
                _sawmill.Debug($"Book {book} shares coordinates and parent with known GrudgeBearer {uid}; attempting binding.");
                if (BindBook(book, uid))
                    return;
                // in the unlikely event that someone spawns a bunch of dwarfs on the same tile and their inventories all overflow,
                // this should match them up 1:1... so long as this never gets multithreaded :sob:
            }
        }
    }

    private void OnGrudgeBearerSpawn(Entity<GrudgeBearerComponent> entity, ref ComponentStartup ev)
    {
        var inventoryComponent = _entManager.GetComponent<InventoryComponent>(entity);
        var inventoryEnt = WithCompOrNull<InventoryComponent>(entity);
        if (!inventoryEnt.HasValue)
        {
            _sawmill.Debug("GrudgeBearer has no InventoryComponent.");
            return;
        }

        var parent = Transform(entity).ParentUid;

        _sawmill.Debug($"GrudgeBearer spawned; parent is {parent}.");

        var binding = new GrudgeBindEvent();
        _inv.RelayEvent<GrudgeBindEvent>(inventoryEnt.Value, ref binding);
        var ire = new InventoryRelayedEvent<GrudgeBindEvent>(binding, entity.Owner);

        if (!ire.Args.Handled)
            _audioSystem.PlayPredicted(entity.Comp.ErrorSound, entity.Owner, entity.Owner);
    }

    private void BindBookByEvent(Entity<GrudgeBookComponent> entity, ref GrudgeBindEvent ev)
    {
        if (ev.Handled)
            return; // if you spawn with multiple books of grudges, something has gone wrong probably

        _sawmill.Debug($"GrudgeBook {entity} binding by event to GrudgeBearer {ev.Source}.");

        if (BindBook(entity, ev.Source))
            ev.Handled = true;
    }

    private bool BindBook(Entity<GrudgeBookComponent> book, EntityUid bearer)
    {
        if (!TryComp<GrudgeBearerComponent>(bearer, out var bearerComp))
        {
            _sawmill.Debug($"Can't bind GrudgeBook {book.Owner} to {bearer}: not a GrudgeBearer.");
            return false;
        }

        book.Comp.RightfulOwner = bearer;
        bearerComp.Book = book;

        _sawmill.Debug($"Successfully bound GrudgeBook {book.Owner} to GrudgeBearer {bearer}.");

        // write preamble
        if (!TryComp<PaperComponent>(book, out var paperComp))
            return false;

        paperComp.Content = Loc.GetString(book.Comp.preamble, ("owner", book.Comp.RightfulOwner));
        TryDirty(book);

        // iterate over crew
        var hits = 0;
        if (false) { // TODO: iterate over crew
            var target = book; // TODO
            if(HasComp<HumanoidAppearanceComponent>(target)) {
                if(GenerateGrudge(book, target))
                    hits += 1;
            }
        }

        if (hits > 0)
            _audioSystem.PlayPredicted(bearerComp.GrudgeSound, book, book);

        return true;
    }

    private void OnGrudgeBookShutdown(Entity<GrudgeBookComponent> entity, ref ComponentShutdown ev)
    {
        // i am not sure this is even necessary
        if (!TryComp<GrudgeBearerComponent>(entity.Comp.RightfulOwner, out var bearerComp))
            return;

        bearerComp.Book = null;
    }

    private void OnGrudgeBearerShutdown(Entity<GrudgeBearerComponent> entity, ref ComponentShutdown ev)
    {
        // i am not sure this is even necessary
        if (!TryComp<GrudgeBookComponent>(entity.Comp.Book, out var bookComp))
            return;

        bookComp.RightfulOwner = null;
    }
}

/// <summary>
///     When a GrudgeBearer spawns, they send out a scan 'pulse' to their inventory in search of GrudgeBooks.
///     Any GrudgeBook detected this way accepts the source GrudgeBearer as its RightfulOwner.
/// </summary>
public sealed partial class GrudgeBindEvent : HandledEntityEventArgs, IInventoryRelayEvent
{
    /// <summary>
    ///     The entity to whom any handling GrudgeBooks will be bound.
    /// </summary>
    public EntityUid Source;

    public SlotFlags TargetSlots { get; } = SlotFlags.All;
}

using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._Starlight.Magic.Components;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Speech;
using Content.Server.Speech;
using Content.Server.Actions;
using Content.Shared.Clothing;
using Content.Server.GameTicking.Rules;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;
using System.Linq;
using Content.Server.Chat.Managers;

namespace Content.Server._Starlight.Magic.Systems;

public sealed class WizardBattleSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private static readonly string[] RecruitmentWords = { "abracadabra", "alakazam", "hocus pocus", "presto chango", "sim sala bim" };
    private static readonly string[] Syllables = { "ab", "ad", "ag", "ak", "al", "an", "ap", "ar", "as", "at", "ax", "az", "ba", "be", "bi", "bo", "bu", "ca", "ce", "ci", "co", "cu", "da", "de", "di", "do", "du", "ea", "eb", "ec", "ed", "ef", "eg", "eh", "ei", "ej", "ek", "el", "em", "en", "eo", "ep", "eq", "er", "es", "et", "eu", "ev", "ew", "ex", "ey", "ez", "fa", "fe", "fi", "fo", "fu", "ga", "ge", "gi", "go", "gu", "ha", "he", "hi", "ho", "hu", "ia", "ib", "ic", "id", "ie", "if", "ig", "ih", "ii", "ij", "ik", "il", "im", "in", "io", "ip", "iq", "ir", "is", "it", "iu", "iv", "iw", "ix", "iy", "iz", "ja", "je", "ji", "jo", "ju", "ka", "ke", "ki", "ko", "ku", "la", "le", "li", "lo", "lu", "ma", "me", "mi", "mo", "mu", "na", "ne", "ni", "no", "nu", "oa", "ob", "oc", "od", "oe", "of", "og", "oh", "oi", "oj", "ok", "ol", "om", "on", "oo", "op", "oq", "or", "os", "ot", "ou", "ov", "ow", "ox", "oy", "oz", "pa", "pe", "pi", "po", "pu", "qa", "qe", "qi", "qo", "qu", "ra", "re", "ri", "ro", "ru", "sa", "se", "si", "so", "su", "ta", "te", "ti", "to", "tu", "ua", "ub", "uc", "ud", "ue", "uf", "ug", "uh", "ui", "uj", "uk", "ul", "um", "un", "uo", "up", "uq", "ur", "us", "ut", "uu", "uv", "uw", "ux", "uy", "uz", "va", "ve", "vi", "vo", "vu", "wa", "we", "wi", "wo", "wu", "xa", "xe", "xi", "xo", "xu", "ya", "ye", "yi", "yo", "yu", "za", "ze", "zi", "zo", "zu" };

    private List<string> IncantationWords = new();
    private Dictionary<string, EntityUid> FactionArchmages = new();
    private const int RitualRange = 10; // tiles

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WizardBattleArchmageComponent, ComponentStartup>(OnArchmageStartup);
        SubscribeLocalEvent<WizardBattleApprenticeComponent, ComponentStartup>(OnApprenticeStartup);
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<WizardBattleArchmageComponent, ComponentRemove>(OnArchmageRemoved);
        SubscribeLocalEvent<WizardBattleApprenticeComponent, ComponentRemove>(OnApprenticeRemoved);
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnRoundStart);
    }

    private void OnRoundStart(RoundStartAttemptEvent ev)
    {
        // Generate unique incantation
        IncantationWords.Clear();
        for (int i = 0; i < 5; i++) // 5 words
        {
            var word = GenerateWord();
            while (IncantationWords.Contains(word))
                word = GenerateWord();
            IncantationWords.Add(word);
        }
    }

    private string GenerateWord()
    {
        var length = _random.Next(2, 5);
        var word = "";
        for (int i = 0; i < length; i++)
        {
            word += _random.Pick(Syllables);
        }
        return word;
    }

    private void OnArchmageStartup(EntityUid uid, WizardBattleArchmageComponent component, ComponentStartup args)
    {
        // Assign faction randomly if not set
        if (component.Faction == "Red")
        {
            component.Faction = _random.Next(0, 2) == 0 ? "Red" : "Blue";
        }

        // Generate personal recruitment word
        component.RecruitmentWord = _random.Pick(RecruitmentWords);

        // Register as faction archmage
        FactionArchmages[component.Faction] = uid;

        // Set the correct scarf
        var scarfProto = component.Faction == "Red" ? "ClothingNeckScarfWizardRed" : "ClothingNeckScarfWizardBlue";
        // TODO: Replace the scarf in inventory and add all all scarfs for recruitment

        // Give wizcoins based on recruits scaling
        UpdateArchmageWizcoins(uid, component);

        // Inform the archmage of their recruitment word (preferably also in UI somewhere)
        _popup.PopupEntity($"Your recruitment word is: {component.RecruitmentWord}", uid, uid);
    }

    private void OnApprenticeStartup(EntityUid uid, WizardBattleApprenticeComponent component, ComponentStartup args)
    {
        // Assign random spell
        var spells = new[] { "ActionBlink", "ActionChargeSpell", "ActionSmite", "ActionFireball", "ActionAnimateSpell", "ActionRepulse", "ActionVoidApplause" };
        component.Spell = _random.Pick(spells);

        // Grant the spell action
        _actions.AddAction(uid, component.Spell);

        // Inform the apprentice of the recruitment word if they have an archmage
        if (component.Archmage != null && TryComp<WizardBattleArchmageComponent>(component.Archmage.Value, out var archmageComp))
        {
            _popup.PopupEntity($"Your archmage's recruitment word is: {archmageComp.RecruitmentWord}", uid, uid);
        }
    }

    // Handle speech for recruitment and rituals, maybe it does not need to be here
    private void OnEntitySpoke(EntitySpokeEvent args) 
    {
        // Check if archmage attempting ritual
        if (TryComp<WizardBattleArchmageComponent>(args.Source, out var archmageComp))
        {
            var message = args.Message.Trim().ToLowerInvariant();
            var incantation = string.Join(" ", archmageComp.RitualWords);
            if (message == incantation)
            {
                TryPerformRitual(args.Source, archmageComp);
                return;
            }
        }

        // Check if wearing a wizard battle scarf
        var enumerator = _inventory.GetSlotEnumerator(args.Source, SlotFlags.NECK);
        EntityUid? scarf = null;
        string? faction = null;
        while (enumerator.MoveNext(out var containerSlot))
        {
            if (containerSlot.ContainedEntity is { } item && TryComp<WizardBattleScarfComponent>(item, out var scarfComp))
            {
                scarf = item;
                faction = scarfComp.Faction;
                break;
            }
        }

        if (scarf == null || faction == null)
            return;

        var recruitMessage = args.Message.ToLowerInvariant();
        if (FactionArchmages.TryGetValue(faction, out var archmage) && TryComp<WizardBattleArchmageComponent>(archmage, out var archComp) && recruitMessage.Contains(archComp.RecruitmentWord))
        {
            TryRecruit(args.Source, faction);
        }
    }

    private void TryPerformRitual(EntityUid archmage, WizardBattleArchmageComponent component)
    {
        // Check if has full incantation
        if (component.RitualWords.Count < IncantationWords.Count)
        {
            _popup.PopupEntity("You do not have the full incantation yet!", archmage, archmage);
            return;
        }

        // Check if on main station (not on shuttle)
        var transform = Transform(archmage);
        if (transform.GridUid == null || !TryComp<PhysicsComponent>(transform.GridUid, out var physics) || physics.BodyType != BodyType.Static)
        {
            _popup.PopupEntity("You must be on the main station to perform the ritual!", archmage, archmage);
            return;
        }

        // Check if at least half apprentices are present within range
        var presentApprentices = 0;
        var totalApprentices = component.Recruits.Count;
        var archmagePos = transform.Coordinates;
        foreach (var recruit in component.Recruits)
        {
            if (!TryComp<TransformComponent>(recruit, out var recruitTransform))
                continue;
            var distance = (archmagePos.Position - recruitTransform.Coordinates.Position).Length();
            if (distance <= RitualRange)
                presentApprentices++;
        }

        if (presentApprentices < totalApprentices / 2.0f)
        {
            _popup.PopupEntity($"You need at least half your apprentices present! ({presentApprentices}/{totalApprentices})", archmage, archmage);
            return;
        }

        // TODO: Check for materials

        // Win the battle
        WinBattle(component.Faction);
    }

    private void WinBattle(string faction)
    {
        // TODO: Give scarves to all players
        // TODO: End round or announce winner
        _chat.SendAdminAnnouncement($"The {faction} faction has won the Wizard Battle!");
    }

    private void TryRecruit(EntityUid target, string faction)
    {
        // Check if already recruited
        if (TryComp<WizardBattleApprenticeComponent>(target, out var existingApprentice))
        {
            // If same faction, do nothing
            if (existingApprentice.Faction == faction)
                return;

            // If different faction, switch sides
            SwitchApprenticeFaction(target, faction);
            return;
        }

        // New recruit
        var apprenticeComp = EnsureComp<WizardBattleApprenticeComponent>(target);
        apprenticeComp.Faction = faction;

        // Find the archmage for this faction
        var archmage = FindArchmageForFaction(faction);
        if (archmage != null)
        {
            apprenticeComp.Archmage = archmage;
            var archmageComp = Comp<WizardBattleArchmageComponent>(archmage.Value);
            archmageComp.Recruits.Add(target);

            // Update wizcoins
            UpdateArchmageWizcoins(archmage.Value, archmageComp);

            // Grant spell
            OnApprenticeStartup(target, apprenticeComp, new ComponentStartup());
        }

        _popup.PopupEntity(Loc.GetString("wizard-battle-recruited", ("faction", faction)), target, target);
    }

    private void SwitchApprenticeFaction(EntityUid apprentice, string newFaction)
    {
        if (!TryComp<WizardBattleApprenticeComponent>(apprentice, out var apprenticeComp))
            return;

        var oldFaction = apprenticeComp.Faction;
        apprenticeComp.Faction = newFaction;

        // Remove from old archmage
        if (apprenticeComp.Archmage != null && TryComp<WizardBattleArchmageComponent>(apprenticeComp.Archmage.Value, out var oldArchmageComp))
        {
            oldArchmageComp.Recruits.Remove(apprentice);
            UpdateArchmageWizcoins(apprenticeComp.Archmage.Value, oldArchmageComp);
        }

        // Add to new archmage
        var newArchmage = FindArchmageForFaction(newFaction);
        if (newArchmage != null)
        {
            apprenticeComp.Archmage = newArchmage;
            var newArchmageComp = Comp<WizardBattleArchmageComponent>(newArchmage.Value);
            newArchmageComp.Recruits.Add(apprentice);
            UpdateArchmageWizcoins(newArchmage.Value, newArchmageComp);
        }

        _popup.PopupEntity(Loc.GetString("wizard-battle-switched", ("oldFaction", oldFaction), ("newFaction", newFaction)), apprentice, apprentice);
    }

    private EntityUid? FindArchmageForFaction(string faction)
    {
        return FactionArchmages.TryGetValue(faction, out var archmage) ? archmage : null;
    }

    private void UpdateArchmageWizcoins(EntityUid archmage, WizardBattleArchmageComponent component)
    {
        // For now, just give base 5 + 2 per recruit TODO MAKE IT UPDATE PROPERLY AND NOT OVERWRITE
        var wizcoins = 8 + component.Recruits.Count * 2;
        // TODO: Actually set wizcoins in their store

        // Grant ritual words based on recruit count
        var wordIndex = component.Recruits.Count / component.NextWordThreshold;
        while (component.RitualWords.Count < wordIndex && component.RitualWords.Count < IncantationWords.Count)
        {
            component.RitualWords.Add(IncantationWords[component.RitualWords.Count]);
            // TODO: Update objective text
        }
    }

    private void OnArchmageRemoved(EntityUid uid, WizardBattleArchmageComponent component, ComponentRemove args)
    {
        // Remove from faction archmages
        FactionArchmages.Remove(component.Faction);

        // Remove all recruits
        foreach (var recruit in component.Recruits)
        {
            if (TryComp<WizardBattleApprenticeComponent>(recruit, out var apprenticeComp))
            {
                apprenticeComp.Archmage = null;
                // Maybe remove apprentice component or make them neutral
            }
        }
    }

    private void OnApprenticeRemoved(EntityUid uid, WizardBattleApprenticeComponent component, ComponentRemove args)
    {
        // Remove from archmage's list
        if (component.Archmage != null && TryComp<WizardBattleArchmageComponent>(component.Archmage.Value, out var archmageComp))
        {
            archmageComp.Recruits.Remove(uid);
            UpdateArchmageWizcoins(component.Archmage.Value, archmageComp);
        }
    }
}
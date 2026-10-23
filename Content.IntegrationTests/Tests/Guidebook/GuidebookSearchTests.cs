#nullable enable

using System.Linq;
using Content.Client._Starlight.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.UserInterface.Controls.FancyTree;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Decals;
using Content.Shared.Guidebook;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Guidebook;

/// <summary>
///     Checks guidebook navigation, keyword coverage, and category colors using loaded game prototypes.
/// </summary>
[TestFixture]
[TestOf(typeof(GuidebookWindow))]
public sealed class GuidebookSearchTests : GameTest
{
    private static readonly ProtoId<ColorPalettePrototype> DepartmentalPalette = "Departmental";

    /// <summary>
    ///     Verifies SOP grouping, Command role matches, and departmental color overrides.
    /// </summary>
    [Test]
    public async Task DepartmentSearchGroupsSopAndIncludesCommandHeads()
    {
        await Client.WaitAssertion(() =>
        {
            using var window = new GuidebookWindow();
            var entries = CProtoMan.EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(entry => new ProtoId<GuideEntryPrototype>(entry.Id), entry => (GuideEntry)entry);
            window.UpdateGuides(entries, new() { "SOP", "Command" });

            TreeItem Item(string id) => window.Tree.Items.Single(item => item.Metadata is GuideEntry entry && entry.Id == id);
            var search = window.FindControl<LineEdit>("CategorySearchBar");
            search.SetText("sop", invokeEvent: true);
            var visibleRoots = window.Tree.Body.Children.OfType<TreeItem>().Where(item => item.Visible).ToArray();
            Assert.That(visibleRoots, Has.Length.EqualTo(1));
            Assert.That(visibleRoots[0], Is.SameAs(Item("SOP")));
            Assert.That(window.Tree.Items.Where(item => item != Item("Command") &&
                item != Item("secure-terminal-guide")).All(item => item.Visible), Is.True);

            search.SetText("command", invokeEvent: true);
            var commandRoles = new[]
            {
                "captain", "security-sop-headofsecurity", "science-sop-researchdirector",
                "cargo-sop-quartermaster", "engineering-sop-chief-engineer",
                "medical-sop-chiefmedicalofficer", "service-sop-headofpersonnel",
            };
            foreach (var role in commandRoles)
                Assert.That(Item(role).Visible, Is.True, $"Command search should include {role}");
            Assert.That(Item("security-sop-cadet").Visible, Is.False);

            var departments = new (string Entry, string Department, string? Preset)[]
            {
                ("command-sop", "Command", "command"), ("security-sop-intro", "Security", "security"),
                ("engineering-sop-intro", "Engineering", "engineering"), ("medical-sop-intro", "Medical", "medical"),
                ("science-sop-intro", "Science", "science"), ("cargo-sop-intro", "Cargo", "cargo"),
                ("service-sop-intro", "Civilian", "service"), ("salvage-sop-intro", "ITG", "salvage"),
                ("ai", "Silicon", null), ("nano-trasen-employee-sop-intro", "Representatives", null),
            };
            var palette = CProtoMan.Index(DepartmentalPalette);
            foreach (var (entry, department, preset) in departments)
            {
                var color = preset == null
                    ? CProtoMan.Index<DepartmentPrototype>(department).Color
                    : palette.Colors[preset].WithAlpha(1f);
                Assert.That(EntryMarkerColor(Item(entry)), Is.EqualTo(color), entry);
            }

            foreach (var entry in new[] { "nano-trasen-employee-sop-magistrate", "nano-trasen-employee-sop-iaa", "service-sop-lawyer" })
                Assert.That(Item(entry).Label.FontColorOverride, Is.EqualTo(Color.FromHex("#C45C8B")), entry);

            // Dark departmental text must retain its blue saturation instead of becoming gray.
            foreach (var entry in new[] { "command-sop", "nano-trasen-employee-sop-intro" })
            {
                var source = Color.ToHsl(EntryMarkerColor(Item(entry)));
                var text = Color.ToHsl(Item(entry).Label.FontColorOverride!.Value);
                Assert.That(text.X, Is.EqualTo(source.X).Within(0.001f), entry);
                Assert.That(text.Y, Is.EqualTo(source.Y).Within(0.001f), entry);
                Assert.That(text.Z, Is.GreaterThan(source.Z), entry);
            }

            Assert.That(Item("command-sop").Label.FontColorOverride,
                Is.Not.EqualTo(Item("nano-trasen-employee-sop-intro").Label.FontColorOverride));
            Assert.That(EntryMarkerColor(Item("captain")), Is.EqualTo(EntryMarkerColor(Item("command-sop"))));
            Assert.That(EntryMarkerColor(Item("nano-trasen-employee-sop-nt-rep")),
                Is.EqualTo(EntryMarkerColor(Item("nano-trasen-employee-sop-intro"))));
        });
    }

    /// <summary>
    ///     Checks that every guide has catalog keywords and representative aliases find the expected pages.
    /// </summary>
    [Test]
    public async Task KeywordCatalogCoversEveryGuideEntry()
    {
        await Client.WaitAssertion(() =>
        {
            var localizationManager = Client.ResolveDependency<ILocalizationManager>();
            using (Assert.EnterMultipleScope())
            {
                foreach (var entry in CProtoMan.EnumeratePrototypes<GuideEntryPrototype>())
                {
                    var hasKeywords = localizationManager.TryGetString(
                        $"guidebook-search-keywords-{entry.Id.ToLowerInvariant()}",
                        out var keywords);
                    Assert.That(hasKeywords && !string.IsNullOrWhiteSpace(keywords), Is.True,
                        $"Missing search keywords for {entry.Id}");
                }
            }

            var examples = new (string Id, string Query)[]
            {
                ("CorporateLaw", "spacelaw"),
                ("Atmospherics", "atmos"),
                ("Supermatter", "sm"),
                ("Medical", "medbay"),
                ("Botany", "gardening"),
                ("NuclearOperatives", "nukies"),
                ("service-sop-headofpersonnel", "hop"),
                ("cargo-sop-quartermaster", "qm"),
                ("science-sop-researchdirector", "rd"),
                ("security-sop-headofsecurity", "hos"),
                ("Controls", "keybindings"),
                ("NuclearReactor", "fission"),
                ("SlimePerson", "slime person"),
                ("RuleC5", "metacomms"),
            };

            foreach (var (id, query) in examples)
            {
                var entry = CProtoMan.Index<GuideEntryPrototype>(id);
                Assert.That(GuidebookSearchEntry.FromEntry(entry, localizationManager).Matches(query), Is.True,
                    $"Searching for '{query}' should find {id}");
            }
        });
    }

    /// <summary>
    ///     Checks nested results, missing-result messages, and navigation to filtered-out entries.
    /// </summary>
    [Test]
    public async Task SearchFindsNestedEntriesAndHandlesMissingResults()
    {
        await Client.WaitAssertion(() =>
        {
            using var window = new GuidebookWindow();
            var ids = new[]
            {
                "CorporateLaw", "LegalRepresentation", "PleaBargains", "Command", "Lawsets",
                "service-sop-intro", "service-sop-staff-intro", "service-sop-lawyer",
                "nano-trasen-employee-sop-magistrate",
            };
            var entries = ids.Select(id => CProtoMan.Index<GuideEntryPrototype>(id))
                .ToDictionary(entry => new ProtoId<GuideEntryPrototype>(entry.Id), entry => (GuideEntry)entry);
            window.UpdateGuides(entries);
            window.Tree.SetAllExpanded(false);

            TreeItem Item(string id) => window.Tree.Items.Single(item => item.Metadata is GuideEntry entry && entry.Id == id);
            var search = window.FindControl<LineEdit>("CategorySearchBar");
            var noResults = window.FindControl<Label>("CategoryNoResultsLabel");

            // A nested match must be revealed, while unrelated siblings and branches are hidden.
            search.SetText("plea", invokeEvent: true);
            Assert.That(Item("PleaBargains").Visible, Is.True);
            Assert.That(Item("CorporateLaw").Expanded, Is.True);
            Assert.That(Item("LegalRepresentation").Expanded, Is.True);
            Assert.That(Item("Command").Visible, Is.False);
            Assert.That(Item("Lawsets").Visible, Is.False);

            search.SetText("", invokeEvent: true);
            Assert.That(window.Tree.Items.All(item => item.Visible && !item.Expanded), Is.True);

            // Check the actual localized aliases loaded from the prototypes.
            search.SetText("spacelaw", invokeEvent: true);
            Assert.That(Item("CorporateLaw").Visible, Is.True);
            Assert.That(Item("Command").Visible, Is.False);
            Assert.That(noResults.Visible, Is.False);

            search.SetText("lawdepartment", invokeEvent: true);
            Assert.That(Item("service-sop-lawyer").Visible, Is.True);
            Assert.That(Item("nano-trasen-employee-sop-magistrate").Visible, Is.True);

            search.SetText("space law handbook", invokeEvent: true);
            Assert.That(noResults.Visible, Is.True);
            Assert.That(window.Tree.Items.All(item => !item.Visible), Is.True);

            search.SetText("qzxv", invokeEvent: true);
            Assert.That(noResults.Visible, Is.True);
            Assert.That(window.Tree.Items.All(item => !item.Visible), Is.True);

            // Following a document link must reveal its destination even when it was filtered out.
            window.HandleClick("PleaBargains");
            Assert.That(search.Text, Is.Empty);
            Assert.That(window.Tree.SelectedItem, Is.SameAs(Item("PleaBargains")));
            Assert.That(Item("PleaBargains").Visible, Is.True);

            // Colors survive filtering and follow the category, with explicit overrides for legal roles.
            var lawColor = Item("CorporateLaw").Label.FontColorOverride;
            Assert.That(lawColor, Is.EqualTo(CProtoMan.Index(DepartmentalPalette).Colors["security"].WithAlpha(1f)));
            Assert.That(Item("PleaBargains").Label.FontColorOverride, Is.EqualTo(lawColor));
            Assert.That(Item("Command").Label.FontColorOverride, Is.Not.EqualTo(lawColor));
            Assert.That(Item("service-sop-staff-intro").Label.FontColorOverride,
                Is.EqualTo(Item("service-sop-intro").Label.FontColorOverride));
            Assert.That(Item("service-sop-lawyer").Label.FontColorOverride, Is.EqualTo(Color.FromHex("#C45C8B")));
        });
    }

    /// <summary>
    ///     Reads the opaque category marker separately from its readability-adjusted label color.
    /// </summary>
    private static Color EntryMarkerColor(TreeItem item)
    {
        var marker = item.Label.Parent!.Children.OfType<PanelContainer>().Single();
        return ((StyleBoxFlat)marker.PanelOverride!).BackgroundColor;
    }
}

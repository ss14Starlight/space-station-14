using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.Eui;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Content.Shared.Administration.AutoModEuiMsg;
using static Content.Shared.Administration.AutoModEuiState;
using static Content.Shared.Administration.PermissionsEuiMsg;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Administration.UI
{
    [UsedImplicitly]
    public sealed class AutoModEui : BaseEui
    {
        private readonly Menu _menu;
        private AutoModEuiState recentState = default!;
        public AutoModEui()
        {
            IoCManager.InjectDependencies(this);

            _menu = new Menu(this);
            _menu.RulesList.GenerateItem = GenerateItem;

            _menu.refresh.OnPressed += args =>
            {
                SendMessage(new RefreshRequest());
            };

            _menu.addRuleButton.OnPressed += args =>
            {
                // Make a blank rule and add to local state only
                var rule = new AutoModRule();
                rule.Enabled = false;
                recentState.Rules.Add(rule);
                var data = recentState.Rules.Select(r => new AutoModListData(r)).ToList();
                _menu.RulesList.PopulateList(data);
            };

            _menu.saveAllButton.OnPressed += args =>
            {
                //send message to save all rules
                SendMessage(new BulkUpdateRulesRequest(recentState.Rules));
            };
        }
        public override void Closed()
        {
            base.Closed();

            SendMessage(new CloseEuiMessage());
            _menu.Close();
        }
        public override void Opened()
        {
            _menu.OpenCentered();
        }

        public override void HandleState(EuiStateBase state)
        {
            base.HandleState(state);
            
            recentState = (AutoModEuiState)state;

            if (recentState == null)
                return;

            var data = recentState.Rules.Select(rule => new AutoModListData(rule)).ToList();
            _menu.RulesList.PopulateList(data);
        }

        private void GenerateItem(ListData data, ListContainerButton button)
        {
            button.RemoveAllChildren();

            var rule = (AutoModListData)data;

            var itemBox = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true };
            var topRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
            var bottomRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };

            // Regex field
            var regex = new LineEdit
            {
                Text = rule.rule.Regex ?? string.Empty,
                PlaceHolder = Loc.GetString("automod-pattern-placeholder"),
                HorizontalExpand = true,
                VerticalExpand = true,
                MinSize = new Vector2(200, 0)
            };
            regex.OnTextChanged += args => {
                rule.rule.Regex = regex.Text;
            };
            topRow.AddChild(regex);
            itemBox.AddChild(topRow);

            // Offences UI
            if (rule.rule.Offences == null)
                rule.rule.Offences = new List<AutoModOffence>();

            var offencesVBox = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };
            int offenceIndex = 0;
            foreach (var offence in rule.rule.Offences.ToList())
            {
                var offenceRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
                var offenceLabel = new Label { Text = Loc.GetString("automod-offence-label", ("index", (offenceIndex + 1).ToString())), HorizontalExpand = false, MinSize = new Vector2(70, 0) };
                var offenceMsg = new LineEdit
                {
                    Text = offence.Message ?? string.Empty,
                    MinSize = new Vector2(150, 0)
                };
                offenceMsg.OnTextChanged += args => {
                    offence.Message = offenceMsg.Text;
                };

                var actionDropdown = new OptionButton { HorizontalExpand = false, MinSize = new Vector2(100, 0) };
                foreach (var action in Enum.GetValues(typeof(AutoModOffenceAction)).Cast<AutoModOffenceAction>())
                {
                    if (action == AutoModOffenceAction.Clear)
                        continue;
                    actionDropdown.AddItem(action.ToString(), (int)action);
                }
                actionDropdown.SelectId((int)offence.Action);
                actionDropdown.OnItemSelected += args => {
                    offence.Action = (AutoModOffenceAction)args.Id;
                    actionDropdown.SelectId((int)offence.Action);
                };

                // Add CancelSpeech toggle for this offence
                var cancelSpeechToggle = new CheckBox
                {
                    Pressed = offence.CancelSpeech,
                    HorizontalExpand = false,
                    Text = Loc.GetString("automod-cancel-speech"),
                    Margin = new Thickness(5, 0, 0, 0)
                };
                cancelSpeechToggle.OnToggled += args => {
                    offence.CancelSpeech = cancelSpeechToggle.Pressed;
                };

                // Ban duration moved below action dropdown for visibility
                var banDurationVBox = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = false };
                var banDurationLabel = new Label { Text = Loc.GetString("automod-ban-duration-label"), HorizontalExpand = false };
                var banDurationEdit = new LineEdit
                {
                    Text = offence.BanDurationMinutes.ToString(),
                    HorizontalExpand = false,
                    MinSize = new Vector2(100, 0)
                };
                banDurationEdit.OnTextChanged += args => {
                    if (int.TryParse(banDurationEdit.Text, out var val))
                        offence.BanDurationMinutes = val;
                };
                banDurationVBox.AddChild(banDurationLabel);
                banDurationVBox.AddChild(banDurationEdit);

                var decayHeader = new Label
                {
                    Text = Loc.GetString("automod-decay-label"),
                    HorizontalExpand = false,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                var decayEdit = new LineEdit
                {
                    Text = offence.DecaySeconds.ToString(),
                    HorizontalExpand = false,
                    MinSize = new Vector2(100, 0)
                };
                decayEdit.OnTextChanged += args => {
                    if (int.TryParse(decayEdit.Text, out var val))
                        offence.DecaySeconds = val;
                };

                var removeBtn = new Button { Text = "-", HorizontalExpand = false, MinSize = new Vector2(30, 0) };
                removeBtn.OnPressed += _ => {
                    rule.rule.Offences.Remove(offence);
                    _menu.RulesList.PopulateList(recentState.Rules.Select(r => new AutoModListData(r)).ToList());
                };
                offenceRow.AddChild(offenceLabel);
                offenceRow.AddChild(offenceMsg);
                offenceRow.AddChild(actionDropdown);
                offenceRow.AddChild(cancelSpeechToggle);
                offenceRow.AddChild(banDurationVBox);
                var decayVBox = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = false };
                decayVBox.AddChild(decayHeader);
                decayVBox.AddChild(decayEdit);
                offenceRow.AddChild(decayVBox);
                offenceRow.AddChild(removeBtn);
                offencesVBox.AddChild(offenceRow);
                offenceIndex++;
            }
            var addOffenceBtn = new Button { Text = "+", HorizontalExpand = false, MinSize = new Vector2(30, 0) };
            addOffenceBtn.OnPressed += _ => {
                rule.rule.Offences.Add(new AutoModOffence { Message = "", Action = AutoModOffenceAction.Warn, BanDurationMinutes = 0, DecaySeconds = 0, CancelSpeech = false });
                _menu.RulesList.PopulateList(recentState.Rules.Select(r => new AutoModListData(r)).ToList());
            };
            offencesVBox.AddChild(addOffenceBtn);
            itemBox.AddChild(offencesVBox);

            var enabled = new CheckBox
            {
                Pressed = rule.rule.Enabled,
                HorizontalExpand = true,
                VerticalExpand = true,
                Text = Loc.GetString("automod-enabled"),
            };
            enabled.OnToggled += args => rule.rule.Enabled = enabled.Pressed;

            var deleteButton = new Button
            {
                Text = Loc.GetString("automod-delete-rule"),
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            deleteButton.OnPressed += args =>
            {
                recentState.Rules.Remove(rule.rule);
                var data = recentState.Rules.Select(r => new AutoModListData(r)).ToList();
                _menu.RulesList.PopulateList(data);
            };

            bottomRow.AddChild(enabled);
            bottomRow.AddChild(deleteButton);
            itemBox.AddChild(bottomRow);
            button.AddChild(itemBox);
        }

        private sealed class Menu : DefaultWindow
        {
            private readonly AutoModEui _ui;
            public ListContainer RulesList { get; }
            public Button refresh { get; }
            public Button addRuleButton { get; }
            public Button saveAllButton { get; }
            public Menu(AutoModEui ui)
            {
                _ui = ui;
                Title = Loc.GetString("automod-eui-menu-title");

                var tabs = new TabContainer();


                RulesList = new ListContainer
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };
                // Header row to label the text fields (Pattern/Regex and Message/Reason)
                var headerRow = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                };
                headerRow.AddChild(new Label
                {
                    Text = Loc.GetString("automod-label-pattern"),
                    HorizontalExpand = true,
                });
                headerRow.AddChild(new Label
                {
                    Text = Loc.GetString("automod-label-message"),
                    HorizontalExpand = true,
                });

                var rulesScroll = new ScrollContainer
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    MinSize = new Vector2(600, 300),
                };
                rulesScroll.AddChild(RulesList);

                var rulesVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children = {
                        headerRow,
                        rulesScroll
                    }
                };
                tabs.AddChild(rulesVBox);

                var rulesLowerBarBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                };
                //add a row at the bottom of the window for various controls
                addRuleButton = new Button
                {
                    Text = Loc.GetString("automod-add-rule"),
                    HorizontalExpand = true,
                };
                rulesLowerBarBox.AddChild(addRuleButton);

                //refresh button
                refresh = new Button
                {
                    Text = Loc.GetString("automod-refresh"),
                    HorizontalExpand = true,
                };
                rulesLowerBarBox.AddChild(refresh);

                //save all button
                saveAllButton = new Button
                {
                    Text = Loc.GetString("automod-save-all"),
                    HorizontalExpand = true,
                };
                rulesLowerBarBox.AddChild(saveAllButton);

                //add the row to the bottom of the window
                rulesVBox.AddChild(rulesLowerBarBox);

                var testerVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };

                // Rule Tester UI
                var testerPatternLabel = new Label { Text = Loc.GetString("automod-tester-pattern-label") };
                var testerPatternInput = new LineEdit { PlaceHolder = Loc.GetString("automod-pattern-placeholder"), HorizontalExpand = true };

                var testerTextLabel = new Label { Text = Loc.GetString("automod-tester-text-label") };
                var testerTextInput = new LineEdit { PlaceHolder = Loc.GetString("automod-tester-text-placeholder"), HorizontalExpand = true };

                var testerButton = new Button { Text = Loc.GetString("automod-tester-test-button"), HorizontalExpand = false };
                var testerResult = new RichTextLabel { HorizontalExpand = true, VerticalExpand = true };

                testerButton.OnPressed += _ =>
                {
                    var pattern = testerPatternInput.Text;
                    var text = testerTextInput.Text;

                    if (string.IsNullOrWhiteSpace(pattern))
                    {
                        testerResult.SetMessage(FormattedMessage.FromMarkup(
                            Loc.GetString("automod-tester-error-no-pattern")));
                        return;
                    }

                    try
                    {
                        var regex = new System.Text.RegularExpressions.Regex(pattern);
                        var match = regex.Match(text);

                        if (match.Success)
                        {
                            testerResult.SetMessage(FormattedMessage.FromMarkup(
                                Loc.GetString("automod-tester-match-success", ("match", match.Value))));
                        }
                        else
                        {
                            testerResult.SetMessage(FormattedMessage.FromMarkup(
                                Loc.GetString("automod-tester-no-match")));
                        }
                    }
                    catch (System.ArgumentException ex)
                    {
                        testerResult.SetMessage(FormattedMessage.FromMarkup(
                            Loc.GetString("automod-tester-error-invalid-regex", ("error", ex.Message))));
                    }
                };

                testerVBox.AddChild(testerPatternLabel);
                testerVBox.AddChild(testerPatternInput);
                testerVBox.AddChild(testerTextLabel);
                testerVBox.AddChild(testerTextInput);
                testerVBox.AddChild(testerButton);
                testerVBox.AddChild(testerResult);

                // Regex cheat sheet
                var cheatSheetLabel = new Label { Text = Loc.GetString("automod-tester-cheatsheet-title") };
                var cheatSheet = new RichTextLabel { HorizontalExpand = true, VerticalExpand = true };
                
                // Build the cheat sheet
                var cheatSheetText = new System.Text.StringBuilder();
                for (int i = 1; i <= 12; i++)
                {
                    cheatSheetText.AppendLine(Loc.GetString($"automod-tester-cheatsheet-{i}"));
                }
                cheatSheet.SetMessage(FormattedMessage.FromMarkup(cheatSheetText.ToString()));

                testerVBox.AddChild(cheatSheetLabel);
                testerVBox.AddChild(cheatSheet);

                tabs.AddChild(testerVBox);

                tabs.SetTabTitle(0, Loc.GetString("automod-eui-menu-rules-tab-title"));
                tabs.SetTabTitle(1, Loc.GetString("automod-eui-menu-tester-tab-title"));

                Contents.AddChild(tabs);
            }

            protected override Vector2 ContentsMinimumSize => new Vector2(900, 600);
        }

       internal record AutoModListData(AutoModRule rule) : ListData;
    }
}

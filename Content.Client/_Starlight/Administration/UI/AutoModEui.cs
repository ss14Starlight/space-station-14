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
                //make a blank rule
                var rule = new AutoModRule();
                //ENSURE the rule starts off
                rule.Enabled = false;
                //send message to add rule
                SendMessage(new AddRuleRequest(rule));
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

            //ensure that there is actually a state to use
            if (recentState == null)
                return;

            var data = recentState.Rules.Select(rule => new AutoModListData(rule)).ToList();
            _menu.RulesList.PopulateList(data);
        }

        private void GenerateItem(ListData data, ListContainerButton button)
        {
            var rule = (AutoModListData)data;
            
            var ItemBox = new BoxContainer() { Orientation = LayoutOrientation.Vertical, VerticalExpand = true };
            var TopRow = new BoxContainer() { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
            var BottomRow = new BoxContainer() { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
            
            ItemBox.AddChild(TopRow);
            ItemBox.AddChild(BottomRow);

            //text entry
            var regex = new LineEdit()
            {
                Text = rule.rule.Regex ?? string.Empty,
                PlaceHolder = Loc.GetString("automod-pattern-placeholder"),
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            regex.OnTextChanged += args =>
            {
                //set the regex of the rule
                rule.rule.Regex = regex.Text;
            };

            var severityDropdown = new OptionButton()
            {
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            foreach (var severity in Enum.GetValues(typeof(AutoModSeverity)).Cast<AutoModSeverity>())
            {
                severityDropdown.AddItem(Loc.GetString($"automod-severity-{severity.ToString().ToLower()}"), (int)severity);
            }
            severityDropdown.SelectId((int)rule.rule.Severity); //set the selected item to the current severity
            severityDropdown.OnItemSelected += args =>
            {
                severityDropdown.SelectId(args.Id); //very weird that I have to manually do this....
                //set the severity of the rule
                rule.rule.Severity = (AutoModSeverity)args.Id;
            };

            // Offences UI
            // Ensure offences list exists
            if (rule.rule.Offences == null)
                rule.rule.Offences = new List<AutoModOffence>();

            var offencesVBox = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };
            for (int i = 0; i < rule.rule.Offences.Count; i++)
            {
                var offence = rule.rule.Offences[i];
                var offenceRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
                var offenceLabel = new Label { Text = $"Offence {i + 1}", HorizontalExpand = false };
                var offenceMsg = new LineEdit
                {
                    Text = offence.Message ?? string.Empty,
                    PlaceHolder = Loc.GetString("automod-message-placeholder"),
                    HorizontalExpand = true
                };
                offenceMsg.OnTextChanged += args => offence.Message = offenceMsg.Text;

                var actionDropdown = new OptionButton { HorizontalExpand = false };
                foreach (var action in Enum.GetValues(typeof(AutoModOffenceAction)).Cast<AutoModOffenceAction>())
                {
                    actionDropdown.AddItem(action.ToString(), (int)action);
                }
                actionDropdown.SelectId((int)offence.Action);
                actionDropdown.OnItemSelected += args => offence.Action = (AutoModOffenceAction)args.Id;

                // Ban duration (only relevant for Ban action)
                var banDurationEdit = new LineEdit
                {
                    Text = offence.BanDurationSeconds.ToString(),
                    PlaceHolder = Loc.GetString("Ban duration (seconds, 0=perm)"),
                    HorizontalExpand = false,
                    MinSize = new Vector2(80, 0)
                };
                banDurationEdit.OnTextChanged += args => {
                    if (int.TryParse(banDurationEdit.Text, out var val))
                        offence.BanDurationSeconds = val;
                };

                // Decay timer (not used for first offence)
                // Add a header label for decay
                var decayHeader = new Label
                {
                    Text = Loc.GetString("Decay (seconds, 0=never)"),
                    HorizontalExpand = false,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                var decayEdit = new LineEdit
                {
                    Text = offence.DecaySeconds.ToString(),
                    HorizontalExpand = false,
                    MinSize = new Vector2(80, 0)
                };
                decayEdit.OnTextChanged += args => {
                    if (int.TryParse(decayEdit.Text, out var val))
                        offence.DecaySeconds = val;
                };

                var removeBtn = new Button { Text = "-", HorizontalExpand = false };
                removeBtn.OnPressed += _ => {
                    rule.rule.Offences.Remove(offence);
                    // Force UI refresh
                    _menu.RulesList.PopulateList(recentState.Rules.Select(r => new AutoModListData(r)).ToList());
                };
                offenceRow.AddChild(offenceLabel);
                offenceRow.AddChild(offenceMsg);
                offenceRow.AddChild(actionDropdown);
                offenceRow.AddChild(banDurationEdit);
                var decayVBox = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = false };
                decayVBox.AddChild(decayHeader);
                decayVBox.AddChild(decayEdit);
                offenceRow.AddChild(decayVBox);
                if (rule.rule.Offences.Count > 1) offenceRow.AddChild(removeBtn);
                offencesVBox.AddChild(offenceRow);
            }
            // Add offence button
            var addOffenceBtn = new Button { Text = "+", HorizontalExpand = false };
            addOffenceBtn.OnPressed += _ => {
                rule.rule.Offences.Add(new AutoModOffence { Message = "", Action = AutoModOffenceAction.Clear, BanDurationSeconds = 0, DecaySeconds = 0 });
                _menu.RulesList.PopulateList(recentState.Rules.Select(r => new AutoModListData(r)).ToList());
            };
            offencesVBox.AddChild(addOffenceBtn);

            //disabled for now, needs more database work to be useful
            /* var count = new LineEdit()
            {
                Text = rule.rule.Count.ToString(),
                HorizontalExpand = true,
                VerticalExpand = true,
            }; */

            var enabled = new CheckBox()
            {
                Pressed = rule.rule.Enabled,
                HorizontalExpand = true,
                VerticalExpand = true,
                Text = Loc.GetString("automod-enabled"),
            };
            enabled.OnToggled += args =>
            {
                //set the enabled state of the rule
                rule.rule.Enabled = enabled.Pressed;
            };

            var cancel = new CheckBox()
            {
                Pressed = rule.rule.CancelSpeech,
                HorizontalExpand = true,
                VerticalExpand = true,
                Text = Loc.GetString("automod-cancel-speech"),
            };
            cancel.OnToggled += args =>
            {
                //set the cancel speech state of the rule
                rule.rule.CancelSpeech = cancel.Pressed;
            };

            var deleteButton = new Button()
            {
                Text = Loc.GetString("automod-delete-rule"),
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            deleteButton.OnPressed += args =>
            {
                //send delete message
                SendMessage(new DeleteRuleRequest(rule.rule));
            };

            TopRow.AddChild(regex);
            TopRow.AddChild(offencesVBox);
            BottomRow.AddChild(severityDropdown);
            /* BottomRow.AddChild(count); */
            BottomRow.AddChild(enabled);
            BottomRow.AddChild(cancel);
            BottomRow.AddChild(deleteButton);
            button.AddChild(ItemBox);
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

                var rulesVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children = {
                        headerRow,
                        RulesList
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

            protected override Vector2 ContentsMinimumSize => new Vector2(600, 400);
        }

       internal record AutoModListData(AutoModRule rule) : ListData;
    }
}

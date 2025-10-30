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

            var message = new LineEdit()
            {
                Text = rule.rule.Message ?? string.Empty,
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            message.OnTextChanged += args =>
            {
                //set the message of the rule
                rule.rule.Message = message.Text;
            };

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
            TopRow.AddChild(message);
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
                var rulesVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children = {
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

                tabs.AddChild(testerVBox);
                /* refresh = new Button
                {
                    Text = Loc.GetString("automod-refresh"),
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };

                rulesVBox.AddChild(refresh); */

                tabs.SetTabTitle(0, Loc.GetString("automod-eui-menu-rules-tab-title"));
                tabs.SetTabTitle(1, Loc.GetString("automod-eui-menu-tester-tab-title"));

                Contents.AddChild(tabs);
            }

            protected override Vector2 ContentsMinimumSize => new Vector2(600, 400);
        }

       internal record AutoModListData(AutoModRule rule) : ListData;
    }
}

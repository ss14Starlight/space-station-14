# Main UI
automod-eui-menu-title = Auto Mod
automod-eui-menu-rules-tab-title = Rules
automod-eui-menu-tester-tab-title = Rule Tester
automod-eui-menu-refresh = Refresh (Will lose changes!)
automod-eui-menu-add-rule = Add New Rule
automod-eui-menu-save-all = Save All
admin-player-actions-window-automod = Auto Mod Rules

# Severity
automod-severity-none = None
automod-severity-warning = Warning
automod-severity-kick = Kick
automod-severity-ban = Ban
automod-severity-low = Low (1)
automod-severity-medium = Medium (2)
automod-severity-high = High (3)

# Rules
automod-rule-category = Category:
automod-rule-severity = Severity:
automod-rule-regex = Regex:
automod-rule-regex-placeholder = e.g., (?i)\bword\b
automod-label-pattern = Pattern (Regex)
automod-label-message = Message/Reason
automod-pattern-placeholder = e.g., (?i)\bword\b
automod-message-placeholder = Message/Reason
automod-enabled = Enabled
automod-cancel-speech = Cancel Chat Message
automod-watch-ooc = Watch OOC

# Offences
automod-offence-label = Offence { $index }
automod-offence-heading = Offence {$index}
automod-offence-remove = Remove
automod-rule-add-offence = Add Offence
automod-ban-duration-label = Ban duration (minutes, 0=perm)
automod-decay-label = Decay (seconds, 0=never)
automod-field-action = Action:
automod-field-message = Message:
automod-field-ban-time = Ban Time:
automod-field-decay-time = Decay Time:
automod-field-decay-levels-label = Decay Levels:
automod-persistent = Persistent

# Misc Buttons
automod-add-rule = Add New Rule
automod-delete-rule = Delete Rule
automod-save-all = Save All
automod-refresh = Refresh (Will lose changes!)
automod-ui-ok = OK
automod-ui-close = Close

# Categories
automod-category-uncategorized = Uncategorized
automod-category-enable-all = Enable All
automod-category-disable-all = Disable All
automod-category-delete = Delete Category
automod-category-management-title = Category Management & Statistics
automod-category-default-severity = Default Severity:
automod-category-add-title = Add New Category
automod-category-add-button = Add Category
automod-category-dialog-title = Manage Categories
automod-ui-categories = Categories

# Statistics and Status
automod-rule-statistics = Rule Statistics
automod-rule-never-triggered = Never triggered
automod-rule-total-triggers = Total triggers: {$count}
automod-rule-first-triggered = First triggered: {$time}
automod-rule-last-triggered = Last triggered: {$time}
automod-rule-offences-heading = Offences
automod-window-rules-count = {$count} {$count ->
    [one] rule
    *[other] rules
}
automod-window-active = Active
automod-window-inactive = Inactive
automod-loading-rules = Loading rules...
automod-category-stats-summary = Total Categories: {$categories} | Total Rules: {$rules}
automod-category-rule-stats = {$rules} rules | {$enabled} enabled | {$triggers} total triggers

# Filters
automod-filter-label = Category:
automod-filter-all = All
automod-ui-regex-pattern = Regex Pattern:
automod-ui-test-text = Test Text:
automod-ui-test-button = Test
automod-ui-result = Result:
automod-ui-dropdown-arrow = ▼

# Rule Tester
automod-eui-tester-title = Rule Tester
automod-eui-tester-description = Test your regex patterns against sample text
automod-eui-tester-cheat-sheet = [bold]Regex Quick Reference:[/bold]

• [bold]Common Patterns:[/bold]
• [color=#88ccff](?i)[/color] - Case-insensitive (put at start)
• [color=#88ccff]\\b[/color] - Word boundary (e.g., [color=#88ccff]\\b[/color][color=#ffdd88]word[/color][color=#88ccff]\\b[/color] matches [color=#90ee90]"word"[/color] but not [color=#ff8888]"password"[/color])
• [color=#88ccff]|[/color] - OR (e.g., [color=#88ccff][/color][color=#ffdd88]space[/color][color=#88ccff]|[/color][color=#ffdd88]station[/color] matches [color=#90ee90]"space"[/color] or [color=#90ee90]"station"[/color])
• [color=#88ccff].[/color] - Match any one character (e.g., [color=#ffdd88]a[/color][color=#88ccff].[/color][color=#ffdd88]c[/color] matches [color=#90ee90]"abc"[/color], [color=#90ee90]"a1c"[/color], [color=#90ee90]"a@c"[/color])
• [color=#88ccff]*[/color] - Match 0 or more times (e.g., [color=#ffdd88]a[/color][color=#88ccff]*[/color] matches [color=#90ee90]""[/color], [color=#90ee90]"a"[/color], [color=#90ee90]"aa"[/color], [color=#90ee90]"aaa"[/color])
• [color=#88ccff]+[/color] - Match 1 or more times (e.g., [color=#ffdd88]a[/color][color=#88ccff]+[/color] matches [color=#90ee90]"a"[/color], [color=#90ee90]"aa"[/color], [color=#90ee90]"aaaa"[/color] but not [color=#ff8888]""[/color] or [color=#ff8888]"b"[/color])
• [color=#88ccff]?[/color] - Match 0 or 1 time (e.g., [color=#ffdd88]colou[/color][color=#88ccff]?[/color][color=#ffdd88]r[/color] matches [color=#90ee90]"color"[/color] or [color=#90ee90]"colour"[/color])

[bold]Examples:[/bold]
• Case-insensitive word: [color=#88ccff](?i)\\b[/color][color=#ffdd88]word[/color][color=#88ccff]\\b[/color]
• Multiple words: [color=#88ccff](?i)\\b([/color][color=#ffdd88]space[/color][color=#88ccff]|[/color][color=#ffdd88]station[/color][color=#88ccff]|[/color][color=#ffdd88]fourteen[/color][color=#88ccff])\\b[/color]
• Special characters: [color=#88ccff]\\Q[/color][color=#ffdd88]text[/color][color=#88ccff]\\E[/color]
automod-tester-pattern-label = Pattern (Regex):
automod-tester-text-label = Sample Text:
automod-tester-text-placeholder = Type a message to test...
automod-tester-pattern-placeholder = Enter regex pattern...
automod-tester-test-button = Test
automod-tester-error-no-pattern = Please enter a pattern to test.
automod-tester-error-invalid-regex = [color=red]Invalid regex:[/color] {$error}
automod-tester-match-success = [color=green]Match found:[/color] "{$match}"
automod-tester-match-found = Match found: "{$match}"
automod-tester-no-match = [color=yellow]No match.[/color]

# Blacklist
automod-blacklist-dialog-title = Blacklisted Word Detected
automod-blacklist-dialog-message = Cannot save: Rule contains blacklisted word
    Word: '{$word}'
    Pattern: {$pattern}

# Commands
automod-command-description = Opens the admin AutoMod panel.
automod-command-help = Usage: automod
automod-command-no-server-console = This command cannot be used from the server console.
automod-history-command-description = Shows a player's automod offence history.
automod-history-command-help = Usage: automodhistory <userId or ckey>
    Example: automodhistory CrazyPhantom

# History Display
automod-history-usage = Usage: automodhistory <userId or ckey>
automod-history-player-not-found = Player '{$target}' not found.
automod-history-no-history = No AutoMod history found for '{$target}'.
automod-history-title = AutoMod History for {$target} (UserID: {$userId})
automod-history-separator = ==========================================
automod-history-rule = Rule: {$name}
automod-history-rule-deleted = Rule: #{$id} (DELETED)
automod-history-category = Category: {$category}
automod-history-regex = Regex: {$regex}
automod-history-current-level = Current Level: {$level}
automod-history-total-triggers = Total Triggers: {$count}
automod-history-last-offence = Last Offence: {$time}
automod-history-last-message = Triggered Message: {$message}
automod-history-last-action = Action Taken: {$action}
automod-history-decay-time = Decay Ready In: {$days}d {$hours}h {$minutes}m {$seconds}s
automod-history-decay-levels = Decay Levels: {$levels}
automod-history-decay-ready = Ready to decay {$levels} levels
automod-history-decay-never = No decay configured
automod-history-total-rules = Total Rules Triggered: {$count}
automod-history-total-level-sum = Total Current Level Sum: {$sum}
automod-history-completion-hint = <player name or ckey>
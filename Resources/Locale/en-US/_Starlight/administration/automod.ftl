automod-severity-none = None
automod-severity-warning = Warning
automod-severity-kick = Kick
automod-severity-ban = Ban

automod-enabled = Enabled
automod-cancel-speech = Cancel Chat Message
automod-delete-rule = Delete Rule
automod-save-all = Save All
automod-add-rule = Add New Rule
automod-refresh = Refresh (Will lose changes!)
automod-eui-menu-title = Auto Mod
automod-eui-menu-rules-tab-title = Rules
automod-eui-menu-tester-tab-title = Rule Tester

admin-player-actions-window-automod = Auto Mod Rules
automod-label-pattern = Pattern (Regex)
automod-label-message = Message/Reason
automod-pattern-placeholder = e.g., (?i)\bword\b
automod-message-placeholder = Message/Reason

automod-tester-pattern-label = Pattern (Regex):
automod-tester-text-label = Sample Text:
automod-tester-text-placeholder = Type a message to test...
automod-tester-test-button = Test
automod-tester-error-no-pattern = Please enter a pattern to test.
automod-tester-error-invalid-regex = [color=red]Invalid regex:[/color] {$error}
automod-tester-match-success = [color=green]Match found:[/color] "{$match}"
automod-tester-no-match = [color=yellow]No match.[/color]

automod-tester-cheatsheet-title = Regex Quick Reference:
automod-tester-cheatsheet-1 = • [bold]Common Patterns:[/bold]
automod-tester-cheatsheet-2 = • [color=#88ccff](?i)[/color] - Case-insensitive (put at start)
automod-tester-cheatsheet-3 = • [color=#88ccff]\\b[/color] - Word boundary (e.g., [color=#88ccff]\\b[/color][color=#ffdd88]word[/color][color=#88ccff]\\b[/color] matches [color=#90ee90]"word"[/color] but not [color=#ff8888]"password"[/color])
automod-tester-cheatsheet-4 = • [color=#88ccff]|[/color] - OR (e.g., [color=#88ccff][/color][color=#ffdd88]space[/color][color=#88ccff]|[/color][color=#ffdd88]station[/color] matches [color=#90ee90]"space"[/color] or [color=#90ee90]"station"[/color])
automod-tester-cheatsheet-5 = • [color=#88ccff].[/color] - Match any one character (e.g., [color=#ffdd88]a[/color][color=#88ccff].[/color][color=#ffdd88]c[/color] matches [color=#90ee90]"abc"[/color], [color=#90ee90]"a1c"[/color], [color=#90ee90]"a@c"[/color])
automod-tester-cheatsheet-6 = • [color=#88ccff]*[/color] - Match 0 or more times (e.g., [color=#ffdd88]a[/color][color=#88ccff]*[/color] matches [color=#90ee90]""[/color], [color=#90ee90]"a"[/color], [color=#90ee90]"aa"[/color], [color=#90ee90]"aaa"[/color])
automod-tester-cheatsheet-7 = • [color=#88ccff]+[/color] - Match 1 or more times (e.g., [color=#ffdd88]a[/color][color=#88ccff]+[/color] matches [color=#90ee90]"a"[/color], [color=#90ee90]"aa"[/color], [color=#90ee90]"aaaa"[/color] but not [color=#ff8888]""[/color] or [color=#ff8888]"b"[/color])
automod-tester-cheatsheet-8 = • [color=#88ccff]?[/color] - Match 0 or 1 time (e.g., [color=#ffdd88]colou[/color][color=#88ccff]?[/color][color=#ffdd88]r[/color] matches [color=#90ee90]"color"[/color] or [color=#90ee90]"colour"[/color])
automod-tester-cheatsheet-9 = [bold]Examples:[/bold]
automod-tester-cheatsheet-10 = • Case-insensitive word: [color=#88ccff](?i)\\b[/color][color=#ffdd88]word[/color][color=#88ccff]\\b[/color]
automod-tester-cheatsheet-11 = • Multiple words: [color=#88ccff](?i)\\b([/color][color=#ffdd88]space[/color][color=#88ccff]|[/color][color=#ffdd88]station[/color][color=#88ccff]|[/color][color=#ffdd88]fourteen[/color][color=#88ccff])\\b[/color]
automod-tester-cheatsheet-12 = • Literal special chars: [color=#88ccff]\\Q[/color][color=#ffdd88]\\[Admin][/color][color=#88ccff]\\E[/color]
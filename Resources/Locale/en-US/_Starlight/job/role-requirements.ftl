any-role-required = You require any of the following roles on Discord to play this role: [color=yellow]{$roles}[/color].

# Coloring rule of thumb: limegreen for met requirement, yellow for unmet requirement that can still be met, red for unmeetable

role-timer-department-sufficient = You have [color=limegreen]{TOSTRING($current, "0")}[/color] of the [color=darkgoldenrod]{TOSTRING($required, "0")} required playtime in the [color={$departmentColor}]{$department}[/color] department to play this role.
role-timer-department-insufficient = You have [color=yellow]{TOSTRING($current, "0")}[/color] of the [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] required playtime in the [color={$departmentColor}]{$department}[/color] department to play this role.
role-timer-department-not-too-high = You have [color=limegreen]{TOSTRING($current, "0")}[/color] of at most [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] playtime in the [color={$departmentColor}]{$department}[/color] department to play this role.
role-timer-department-too-high = You have [color=red]{TOSTRING($current, "0")}[/color] of at most [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] playtime in the [color={$departmentColor}]{$department}[/color] department to play this role. (Are you trying to play a trainee role?)

role-timer-overall-sufficient = You have [color=limegreen]{TOSTRING($current, "0")}[/color] of the [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] required total playtime to play this role.
role-timer-overall-insufficient = You have [color=yellow]{TOSTRING($current, "0")}[/color] of [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] required total playtime to play this role.
role-timer-overall-not-too-high = You have [color=limegreen]{TOSTRING($current, "0")}[/color] of at most [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] required total playtime to play this role.
role-timer-overall-too-high = You have [color=red]{TOSTRING($current, "0")}[/color] of at most [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] required total playtime to play this role. (Are you trying to play a trainee role?)

role-timer-role-sufficient = You have [color=limegreen]{TOSTRING($current, "0")}[/color] of the [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] required [color={$departmentColor}]{$job}[/color] playtime to play this role.
role-timer-role-insufficient = You have [color=yellow]{TOSTRING($current, "0")}[/color] of the [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] required [color={$departmentColor}]{$job}[/color] playtime to play this role.
role-timer-role-not-too-high = You have [color=limegreen]{TOSTRING($current, "0")}[/color] of at most [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] [color={$departmentColor}]{$job}[/color] playtime to play this role.
role-timer-role-too-high = You have [color=red]{TOSTRING($current, "0")}[/color] of at most [color=darkgoldenrod]{TOSTRING($required, "0")}[/color] [color={$departmentColor}]{$job}[/color] playtime to play this role. (Are you trying to play a trainee role?)

role-whitelisted = You [color=limegreen]are[/color] whitelisted to play this role.
role-not-whitelisted = You [color=yellow]are not[/color] whitelisted to play this role.

role-timer-age-old-enough = Your character's age must be at least [color=limegreen]{$age}[/color] to play this role.
role-timer-age-not-old-enough = Your character's age must be at least [color=yellow]{$age}[/color] to play this role.
role-timer-age-young-enough = Your character's age must be at most [color=limegreen]{$age}[/color] to play this role.
role-timer-age-not-young-enough = Your character's age must be at most [color=yellow]{$age}[/color] to play this role.

role-timer-whitelisted-species-pass = Your character [color=limegreen]must[/color] be one of the following species to play this role: [color=limegreen]{$species}[/color]
role-timer-whitelisted-species-fail = Your character [color=yellow]must[/color] be one of the following species to play this role: [color=yellow]{$species}[/color]
role-timer-blacklisted-species-pass = Your character [color=limegreen]must not[/color] be one of the following species to play this role: [color=limegreen]{$species}[/color]
role-timer-blacklisted-species-fail = Your character [color=yellow]must not[/color] be one of the following species to play this role: [color=yellow]{$species}[/color]

role-timer-whitelisted-traits-pass = Your character [color=limegreen]must[/color] have one of the following traits: [color=limegreen]{$traits}[/color]
role-timer-whitelisted-traits-fail = Your character [color=yellow]must[/color] have one of the following traits: [color=yellow]{$traits}[/color]
role-timer-blacklisted-traits-pass = Your character [color=limegreen]must not[/color] have one of the following traits: [color=limegreen]{$traits}[/color]
role-timer-blacklisted-traits-fail = Your character [color=yellow]must not[/color] have one of the following traits: [color=yellow]{$traits}[/color]

role-ban = You have been [color=red]banned[/color] from this role.

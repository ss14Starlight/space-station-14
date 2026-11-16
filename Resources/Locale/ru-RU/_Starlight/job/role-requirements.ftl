job-no-requirements = У этой работы нет требований.
ghost-role-no-requirements = У этой роли нет требований.

# Coloring rule of thumb: limegreen for met requirement, yellow for unmet requirement that can still be met, red for unmeetable

role-timer-department-sufficient = У вас [color=limegreen]{ TOSTRING($current, "0") }[/color] из [color=lightblue]{ TOSTRING($required, "0") }[/color] требуемого игрового времени в отделе [color={ $departmentColor }]{ $department }[/color].
role-timer-department-insufficient = У вас [color=yellow]{ TOSTRING($current, "0") }[/color] из [color=lightblue]{ TOSTRING($required, "0") }[/color] требуемого игрового времени в отделе [color={ $departmentColor }]{ $department }[/color].
role-timer-department-not-too-high = У вас [color=limegreen]{ TOSTRING($current, "0") }[/color] из максимум [color=lightblue]{ TOSTRING($required, "0") }[/color] игрового времени в отделе [color={ $departmentColor }]{ $department }[/color].
role-timer-department-too-high = У вас [color=red]{ TOSTRING($current, "0") }[/color] из максимум [color=lightblue]{ TOSTRING($required, "0") }[/color] игрового времени в отделе [color={ $departmentColor }]{ $department }[/color]. (Пытаетесь играть за роль стажёра?)

role-timer-overall-sufficient = У вас [color=limegreen]{ TOSTRING($current, "0") }[/color] из [color=lightblue]{ TOSTRING($required, "0") }[/color] требуемого общего игрового времени.
role-timer-overall-insufficient = У вас [color=yellow]{ TOSTRING($current, "0") }[/color] из [color=lightblue]{ TOSTRING($required, "0") }[/color] требуемого общего игрового времени.
role-timer-overall-not-too-high = У вас [color=limegreen]{ TOSTRING($current, "0") }[/color] из максимум [color=lightblue]{ TOSTRING($required, "0") }[/color] общего игрового времени.
role-timer-overall-too-high = У вас [color=red]{ TOSTRING($current, "0") }[/color] из максимум [color=lightblue]{ TOSTRING($required, "0") }[/color] общего игрового времени. (Пытаетесь играть за роль стажёра?)

role-timer-role-sufficient = У вас [color=limegreen]{ TOSTRING($current, "0") }[/color] из [color=lightblue]{ TOSTRING($required, "0") }[/color] требуемого игрового времени за [color={ $departmentColor }]{ $job }[/color].
role-timer-role-insufficient = У вас [color=yellow]{ TOSTRING($current, "0") }[/color] из [color=lightblue]{ TOSTRING($required, "0") }[/color] требуемого игрового времени за [color={ $departmentColor }]{ $job }[/color].
role-timer-role-not-too-high = У вас [color=limegreen]{ TOSTRING($current, "0") }[/color] из максимум [color=lightblue]{ TOSTRING($required, "0") }[/color] игрового времени за [color={ $departmentColor }]{ $job }[/color].
role-timer-role-too-high = У вас [color=red]{ TOSTRING($current, "0") }[/color] из максимум [color=lightblue]{ TOSTRING($required, "0") }[/color] игрового времени за [color={ $departmentColor }]{ $job }[/color]. (Пытаетесь играть за роль стажёра?)

role-whitelisted = Вы [color=limegreen]включены[/color] в вайтлист для этой роли.
role-not-whitelisted = Вы [color=yellow]не включены[/color] в вайтлист для этой роли.

role-timer-age-old-enough = Возраст вашего персонажа должен быть не менее [color=limegreen]{ $age }[/color], чтобы играть за эту роль.
role-timer-age-not-old-enough = Возраст вашего персонажа должен быть не менее [color=yellow]{ $age }[/color], чтобы играть за эту роль.
role-timer-age-young-enough = Возраст вашего персонажа должен быть не более [color=limegreen]{ $age }[/color], чтобы играть за эту роль.
role-timer-age-not-young-enough = Возраст вашего персонажа должен быть не более [color=yellow]{ $age }[/color], чтобы играть за эту роль.

role-timer-whitelisted-species-pass = Ваш персонаж [color=limegreen]должен[/color] быть одним из следующих видов для этой роли: [color=limegreen]{ $species }[/color]
role-timer-whitelisted-species-fail = Ваш персонаж [color=yellow]должен[/color] быть одним из следующих видов для этой роли: [color=yellow]{ $species }[/color]
role-timer-blacklisted-species-pass = Ваш персонаж [color=limegreen]не должен[/color] быть одним из следующих видов для этой роли: [color=limegreen]{ $species }[/color]
role-timer-blacklisted-species-fail = Ваш персонаж [color=yellow]не должен[/color] быть одним из следующих видов для этой роли: [color=yellow]{ $species }[/color]

role-timer-whitelisted-traits-pass = Ваш персонаж [color=limegreen]должен[/color] иметь одну из следующих черт: [color=limegreen]{ $traits }[/color]
role-timer-whitelisted-traits-fail = Ваш персонаж [color=yellow]должен[/color] иметь одну из следующих черт: [color=yellow]{ $traits }[/color]
role-timer-blacklisted-traits-pass = Ваш персонаж [color=limegreen]не должен[/color] иметь одну из следующих черт: [color=limegreen]{ $traits }[/color]
role-timer-blacklisted-traits-fail = Ваш персонаж [color=yellow]не должен[/color] иметь одну из следующих черт: [color=yellow]{ $traits }[/color]

role-ban = Вы были [color=red]забанены[/color] на эту роль.

role-requirement-any = Соответствуйте хотя бы одному из следующих требований:
role-requirement-none = Не соответствуйте ни одному из следующих требований:

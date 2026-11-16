# The selectors in the case of 1 just don't work for some reason.
# Guess we're always going for plural?

xenobiology-console-monkey-cube-inserted = Спасибо за вставку обезьяньего куба! Теперь на консоли {$cubes} {$cubes ->
    [1] куб
    *[other] куба
    }.

xenobiology-console-mutation-potion-inserted = Спасибо за вставку мутационного зелья! Теперь на консоли {$potions} {$potions ->
    [1] зелье
    *[other] зелий
    }.

xenobiology-console-stabilizer-potion-inserted = Спасибо за вставку стабилизирующего зелья! Теперь на консоли {$potions} {$potions ->
    [1] зелье
    *[other] зелий
    }.

xenobiology-console-slime-picked-up = Подобрано {$name}.
xenobiology-console-slime-picked-up-fail-full = Не удалось подобрать {$name}. Попробуйте выложить часть слаймов.
xenobiology-console-slime-picked-up-fail-none-found = Слаймы не найдены. Попробуйте подойти ближе к одному.

xenobiology-console-slime-placed-down = Выложено {$name}.
xenobiology-console-slime-placed-down-fail-none-stored = Нет хранимых слаймов. Попробуйте подобрать одного.

xenobiology-console-monkey-placed = Выложена обезьяна. Теперь у вас {$cubes} {$cubes ->
    [1] куб
    *[other] куба
    }.
xenobiology-console-monkey-placed-fail-empty = Недостаточно обезьяньих кубов ({$cubes}). Попробуйте вставить один или переработать уже съеденных обезьян.

xenobiology-console-monkey-recycled = Переработано {$monkeys} {$monkeys ->
    [1] обезьяна
    *[other] обезьяны
    }. Теперь у вас {$cubes} {$cubes ->
    [1] куб
    *[other] куба
    }.
xenobiology-console-monkey-recycled-failed-none = Обезьян для переработки не найдено. Подойдите ближе или убедитесь что они достаточно повреждены.

xenobiology-console-mutation-potion-applied = Применено мутационное зелье на {$name}. Теперь шанс мутации {$chance}.
xenobiology-console-mutation-potion-applied-failed-empty = Нет хранимых мутационных зелий. Попробуйте вставить одно.

xenobiology-console-stabilizer-potion-applied = Применено стабилизирующее зелье на {$name}. Теперь шанс мутации {$chance}.
xenobiology-console-stabilizer-potion-applied-failed-empty = Нет хранимых стабилизирующих зелий. Попробуйте вставить одно.

slime-scanner-info = Имя:{"\u0009"}[Bold]{ $name }[/Bold]
    Питание:{"\u0009"}[Bold]{ $nutrition }[/Bold]
    Шанс мутации:{"\u0009"}[Bold]{ $chance }%[/Bold]
xenobiology-camera-tagger-connected = Камера подключена к сети консоли ксенобиологии.

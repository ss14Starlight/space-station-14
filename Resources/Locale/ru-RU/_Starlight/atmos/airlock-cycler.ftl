airlock-cycler-side-a = Сторона А
airlock-cycler-side-b = Сторона Б
airlock-cycler-side-c = Сторона В
airlock-cycler-side-d = Сторона Г

airlock-cycler-button-unlinked = Кнопка не подключена к контроллеру шлюза.
airlock-cycler-no-power = У контроллера шлюза нет питания.
airlock-cycler-access-denied = Доступ запрещён.
airlock-cycler-queued = Запрос в очереди: шлюз занят.

airlock-cycler-window-title = Контроллер шлюза
airlock-cycler-window-chamber = Камера
airlock-cycler-window-target = Цель
airlock-cycler-window-tank = Резервуар
airlock-cycler-window-sides = Направления
airlock-cycler-window-no-sides = Ничего не подключено. Подключите двери к порту стороны мультитулом.
airlock-cycler-window-cycle = Цикл
airlock-cycler-window-force = Открыть сейчас (пропустить давление)
airlock-cycler-window-lockdown = Блокировка
airlock-cycler-window-pressure = {$pressure} кПа
airlock-cycler-window-no-data = Нет данных
airlock-cycler-window-side-info = {$pressure}, двери открыты {$open}/{$doors}
airlock-cycler-window-side-doors = Двери открыты {$open}/{$doors}
airlock-cycler-window-side-queued = В очереди #{$position}

airlock-cycler-state-off = Нет питания
airlock-cycler-state-idle = Готов
airlock-cycler-state-lockdown = Блокировка: все двери заперты
airlock-cycler-state-next = Далее: цикл в сторону {$side}
airlock-cycler-state-closing = Ожидание закрытия дверей ({$side})
airlock-cycler-state-pressurizing = Нагнетание давления в сторону {$side}
airlock-cycler-state-depressurizing = Сброс давления в сторону {$side}

airlock-cycler-problem-no-vent = Только блокировка: не подключён вход или выход.
airlock-cycler-problem-stalled = Давление не меняется: резервуар пуст или полон.
airlock-cycler-problem-obstructed = Дверь заблокирована и не может закрыться.
airlock-cycler-problem-incompatible = Двери ведут к разным давлениям.
airlock-cycler-problem-no-reading = Не удаётся измерить камеру или дверь.
airlock-cycler-problem-unsecured = У двери нет питания, её нельзя запереть.
airlock-cycler-problem-no-inlet = Не удаётся заполнить камеру: не подключён впускной вентилятор.
airlock-cycler-problem-no-outlet = Не удаётся опустошить камеру: не подключён выход.
airlock-cycler-state-holding = Удержание дверей закрытыми ({$side})

ent-AirlockCycler = контроллер шлюза
ent-AirlockCycler-desc = Управляет циклом шлюзовой камеры. Показывает давление за каждой дверью и направление откачки.
ent-AirlockCyclerButton = кнопка цикла шлюза
ent-AirlockCyclerButton-desc = Запрашивает у контроллера шлюза цикл в эту сторону. Зелёный — готово, красный — цикл идёт сюда.
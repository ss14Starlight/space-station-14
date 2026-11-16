## Examine

combustion-turbine-examine-part = Установлена деталь [color=lightblue]уровня { $tier }[/color].
combustion-turbine-examine-no-part = [color=yellow]Отсутствует деталь турбины.[/color]
combustion-turbine-examine-unassembled = [color=yellow]Она не собрана с центральным ротором.[/color]
combustion-turbine-examine-rpm = Ротор вращается со скоростью [color=orange]{ $rpm }[/color] об/мин.
combustion-turbine-examine-integrity = Целостность ротора: [color=orange]{ $integrity }%[/color].
combustion-turbine-examine-power = Она вырабатывает [color=yellow]{ POWERWATTS($power) }[/color].

## Assembly

combustion-turbine-assembly-ready = Турбина полностью собрана.
combustion-turbine-assembly-missing-compressor = [color=yellow]Нужен входной компрессор позади центрального ротора, направленный в ту же сторону.[/color]
combustion-turbine-assembly-missing-outlet = [color=yellow]Нужен выходной патрубок турбины перед центральным ротором, направленный в ту же сторону.[/color]
combustion-turbine-assembly-panel-open = [color=yellow]Сервисная панель на турбине открыта.[/color]
combustion-turbine-assembly-missing-part = [color=yellow]В одной из секций турбины отсутствует деталь.[/color]

## Parts

combustion-turbine-part-wrong-stage = Эта деталь не подходит для этой секции.
combustion-turbine-part-panel-closed = Сначала откройте сервисную панель.
combustion-turbine-part-running = Сначала остановите турбину.
combustion-turbine-part-occupied = Сначала выломайте установленную деталь.
combustion-turbine-part-max-tier = Эта деталь уже максимального уровня.
combustion-turbine-part-wrong-material = Это не тот материал, которым можно улучшить эту деталь.
combustion-turbine-part-not-enough = Для улучшения этой детали нужно { $amount } { $material }.
combustion-turbine-part-upgraded = Теперь деталь имеет уровень { $tier }.
combustion-turbine-part-examine-tier = Деталь [color=lightblue]уровня { $tier }[/color], рассчитанная на [color=orange]{ $rpm }[/color] об/мин и [color=orange]{ $temperature } К[/color].
combustion-turbine-part-examine-compressor = Впуск компрессора: [color=orange]{ $value }%[/color].
combustion-turbine-part-examine-rotor = Трение ротора: [color=orange]{ $value }%[/color].
combustion-turbine-part-examine-stator = Эффективность статора: [color=orange]{ $value }%[/color].
combustion-turbine-part-examine-upgrade = Её можно улучшить с помощью [color=yellow]{ $amount } { $material }[/color].
combustion-turbine-part-examine-max-tier = Она имеет максимальный уровень.

## Radio

combustion-turbine-alert-damage = Внимание: турбина сгорания рядом с { $location } получает повреждения! Целостность ротора { $integrity }%.
combustion-turbine-alert-failure = Турбина сгорания рядом с { $location } получила катастрофический отказ ротора!

## Console

combustion-turbine-console-title = Управление турбиной сгорания
combustion-turbine-console-status-connected = Турбина подключена
combustion-turbine-console-status-unlinked = Турбина не подключена. Подключите центральный ротор мультитулом.
combustion-turbine-console-status-no-compressor = За центральным ротором нет входного компрессора.
combustion-turbine-console-status-no-outlet = Перед центральным ротором нет выходного патрубка турбины.
combustion-turbine-console-status-panel-open = Сервисная панель на турбине открыта.
combustion-turbine-console-status-missing-part = В одной из секций турбины отсутствует деталь.
combustion-turbine-console-power = Мощность турбины
combustion-turbine-console-on = Вкл.
combustion-turbine-console-off = Выкл.
combustion-turbine-console-starter = Пусковой двигатель включён...
combustion-turbine-console-governor = Регулятор скорости удерживает ротор: впуск на { $value }%
combustion-turbine-console-overheat = ПЕРЕГРЕВ: впускной газ горячее, чем рассчитаны детали!
combustion-turbine-console-rpm = Скорость ротора
combustion-turbine-console-rpm-value = { $rpm } / { $max } об/мин
combustion-turbine-console-integrity = Целостность ротора
combustion-turbine-console-output = Выходная мощность
combustion-turbine-console-power-value = { POWERWATTS($power) }
combustion-turbine-console-inlet-temperature = Температура впуска
combustion-turbine-console-temperature-value = { $temperature } К (детали рассчитаны на { $max } К)
combustion-turbine-console-exhaust-temperature = Температура выхлопа
combustion-turbine-console-kelvin-value = { $temperature } К
combustion-turbine-console-inlet-pressure = Давление впуска
combustion-turbine-console-outlet-pressure = Давление выхлопа
combustion-turbine-console-pressure-value = { $pressure } кПа
combustion-turbine-console-pressure-ratio = Степень расширения
combustion-turbine-console-pressure-ratio-value = { $ratio } : 1
combustion-turbine-console-pressure-ratio-vacuum = сброс в вакуум
combustion-turbine-console-pressure-ratio-stalled = заглохла, давление выхлопа слишком высокое
combustion-turbine-console-flow = Поток газа
combustion-turbine-console-flow-value = { $flow } моль/с
combustion-turbine-console-intake = Регулятор впуска
combustion-turbine-console-percent = { $value }%
combustion-turbine-console-intake-value = { $value }% ({ $volume } л/с)
combustion-turbine-console-parts = Уровни деталей
combustion-turbine-console-parts-value = компрессор { $compressor }, ротор { $rotor }, статор { $stator }

combustion-turbine-slot-part = Деталь турбины

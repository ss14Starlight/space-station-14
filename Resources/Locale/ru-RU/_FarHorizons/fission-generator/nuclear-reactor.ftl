### Popups
reactor-smoke-start = {CAPITALIZE(THE($owner))} начинает дымить!
reactor-smoke-stop = {CAPITALIZE(THE($owner))} перестаёт дымить.
reactor-fire-start = {CAPITALIZE(THE($owner))} начинает гореть!
reactor-fire-stop = {CAPITALIZE(THE($owner))} перестаёт гореть.

reactor-unanchor-melted = Нельзя открепить {THE($owner)}, он вплавился в корпус!
reactor-unanchor-warning = Нельзя открепить {THE($owner)}, пока он не пуст или горячее 80C!
reactor-anchor-warning = Недопустимая позиция для закрепления.

### Messages
reactor-smoke-start-message = ТРЕВОГА: {CAPITALIZE(THE($owner))} достиг опасной температуры: {$temperature}K. Немедленно вмешайтесь, чтобы предотвратить расплавление.
reactor-smoke-stop-message = {CAPITALIZE(THE($owner))} остыл ниже опасной температуры. Хорошего дня.
reactor-fire-start-message = ТРЕВОГА: {CAPITALIZE(THE($owner))} достиг КРИТИЧЕСКОЙ температуры: {$temperature}K. РАСПЛАВЛЕНИЕ НЕМИНУЕМО.
reactor-fire-stop-message = {CAPITALIZE(THE($owner))} остыл ниже критической температуры. Расплавление предотвращено.

reactor-temperature-dangerous-message = {CAPITALIZE(THE($owner))} имеет опасную температуру: {$temperature}K.
reactor-temperature-critical-message = {CAPITALIZE(THE($owner))} имеет критическую температуру: {$temperature}K.
reactor-temperature-cooling-message = {CAPITALIZE(THE($owner))} остывает: {$temperature}K.

reactor-melting-announcement = Ядерный реактор на борту станции начинает плавиться. Рекомендуется эвакуировать прилегающую территорию.
reactor-melting-announcement-sender = Ядерная тревога

reactor-meltdown-announcement = Ядерный реактор на борту станции катастрофически перегружен. Вероятны радиоактивные обломки, ядерные осадки и пожары охладителя. Настоятельно рекомендуется немедленно эвакуировать прилегающую территорию.
reactor-meltdown-announcement-sender = Ядерное расплавление

### UI
comp-nuclear-reactor-ui-locked = Заблокировано
comp-nuclear-reactor-ui-insert-button = Вставить
comp-nuclear-reactor-ui-remove-button = Извлечь
comp-nuclear-reactor-ui-eject-button = Выбросить

comp-nuclear-reactor-ui-view-change = Сменить вид
comp-nuclear-reactor-ui-view-temp = Вид температуры
comp-nuclear-reactor-ui-view-neutron = Вид нейтронов
comp-nuclear-reactor-ui-view-fuel = Вид топлива

comp-nuclear-reactor-ui-status-panel = Состояние реактора
comp-nuclear-reactor-ui-reactor-temp = Температура
comp-nuclear-reactor-ui-reactor-rads = Радиация
comp-nuclear-reactor-ui-reactor-therm = Тепловая мощность
comp-nuclear-reactor-ui-reactor-control = Управляющие стержни
comp-nuclear-reactor-ui-therm-format = { POWERWATTS($power) }t

comp-nuclear-reactor-ui-footer-left = Опасно: высокий уровень радиации.
comp-nuclear-reactor-ui-footer-right = 1.0 REV 1

comp-nuclear-reactor-ui-alarm-ack = ПОДТВ.
comp-nuclear-reactor-ui-empty = пусто
comp-nuclear-reactor-ui-fuel-level = Уровень топлива: { $level }%
comp-nuclear-reactor-ui-overload = ПЕРЕГРУЗКА
comp-nuclear-reactor-ui-title = Ядерный реактор

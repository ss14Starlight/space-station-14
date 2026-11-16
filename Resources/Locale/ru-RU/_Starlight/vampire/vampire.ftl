## Base actions

alerts-vampire-blood-name = Выпитая кровь
alerts-vampire-blood-desc = Показывает, сколько крови вы выпили. Выпустите клыки и кликните левой кнопкой по цели, чтобы пить.

alerts-vampire-fed-name = Сытость кровью
alerts-vampire-fed-desc = Ваша текущая сытость кровью. Пейте кровь, чтобы оставаться сытым.

roles-antag-vampire-name = Вампир
roles-antag-vampire-description = Питайтесь экипажем. Выпустите клыки и пейте их кровь.

roles-antag-thrall-name = Тралл
roles-antag-thrall-objective = Верно служите своему хозяину и подчиняйтесь его приказам.

vampire-roundend-name = вампир

vampire-drink-start = Вы вонзаете клыки в {CAPITALIZE(THE($target))}.

vampire-not-enough-blood = Недостаточно крови.

vampire-mouth-covered = Ваш рот закрыт!
vampire-drink-invalid-target = Вы не можете пить кровь вампиров или их траллов.
vampire-target-protected-by-faith = Этот человек защищён своей верой!
vampire-drink-target-empty = В этом существе не осталось крови!
vampire-drink-target-maxed = Вы уже выпили { $amount } ед. крови из этой цели.
vampire-drink-target-hard-max = Вы выпили максимальное количество крови из этой цели ({ $amount } ед.).
vampire-full-power-achieved = Ваша вампирская сущность вспыхивает — полная сила достигнута!
vampire-umbrae-full-power-fov = Тени склоняются перед вашей волей. Теперь вы видите сквозь стены!
vampire-drink-target-not-viable = У этого существа нет бьющегося сердца!
vampire-drink-target-rot = Эссенция этого существа отвратительна!
vampire-sleep-shielded = Это существо нельзя усыпить из-за импланта!
vampire-sleep-protected = Нужен лучший зрительный контакт...

vampire-role-greeting = Вы — вампир!
    Жажда крови заставляет вас питаться членами экипажа. Используйте свои способности, чтобы обращать других членов экипажа.
    Ваши клыки позволяют высасывать кровь из людей. Кровь восстановит здоровье и даст новые способности.
    Найдите себе занятие на эту смену!

# Objectives
objective-issuer-vampire = [color=crimson]Вампир[/color]

objective-condition-drain-title = Выпить {$count} ед. крови
objective-condition-drain-description = Выпейте {$count} ед. крови членов экипажа с помощью клыков.

objective-vampire-thrall-obey-master-title = Подчиняйся своему хозяину, {$targetName}.

# Class selection action
action-vampire-class-select = Выбрать класс вампира
action-vampire-class-select-desc = Выберите свой подкласс вампира

# Round end statistics
roundend-prepend-vampire-drained-low = Вампиры в эту смену почти не питались, выпив всего {$blood} ед. крови.
roundend-prepend-vampire-drained-medium = Вампиры неплохо перекусили, выпив {$blood} ед. крови.
roundend-prepend-vampire-drained-high = У вампиров был кровавый пир, они выпили {$blood} ед. крови!
roundend-prepend-vampire-drained-critical = Вампиры устроили пиршество, выпив ошеломляющие {$blood} ед. крови!

roundend-prepend-vampire-drained = Ни один вампир не смог выпить значимого количества крови в этом раунде.
roundend-prepend-vampire-drained-named = {$name} оказался самым кровожадным вампиром, выпив всего {$number} ед. крови.

# Vampire class selection tooltips
vampire-class-hemomancer-tooltip = Гемомант
    Сосредоточен на магии крови и манипуляции кровью вокруг вас

vampire-class-umbrae-tooltip = Умбра
    Сосредоточен на тьме, скрытных засадах и мобильности

vampire-class-gargantua-tooltip = Гаргантюа
    Сосредоточен на живучести и уроне в ближнем бою

vampire-class-dantalion-tooltip = Данталион
    Сосредоточен на порабощении и иллюзиях

# Hemomancer abilities
action-vampire-hemomancer-tendrils-wrong-place = Сюда нельзя кастовать.

action-vampire-blood-barrier-wrong-place = Сюда нельзя ставить барьеры.

action-vampire-sanguine-pool-already-in = Вы уже в форме кровавой лужи!
action-vampire-sanguine-pool-invalid-tile = Вы не можете стать лужей крови здесь.
action-vampire-sanguine-pool-enter = Вы превращаетесь в лужу крови!
action-vampire-sanguine-pool-exit = Вы восстанавливаетесь из лужи крови!
vampire-space-burn-warning = Суровый свет пустоты сжигает вашу нежить-плоть!

action-vampire-blood-eruption-activated = Вы заставляете кровь извергнуться шипами вокруг вас!

action-vampire-blood-bringers-rite-not-enough-power = Вам не хватает полной вампирской силы (нужно больше 1000 общей крови и 8 уникальных жертв)
action-vampire-blood-brighters-rite-not-enough-blood = Недостаточно крови для активации ритуала носителей крови
action-vampire-blood-bringers-rite-start = Ритуал носителей крови активирован!
action-vampire-blood-bringers-rite-stop = Ритуал носителей крови деактивирован
action-vampire-blood-bringers-rite-stop-blood = Ритуал носителей крови деактивирован — недостаточно крови

vampire-locate-result = Ваши чувства прослеживают { $target } до { $location }.
vampire-locate-not-same-sector = Эта персона не в вашем секторе.
vampire-locate-unknown = Неизвестная область
vampire-locate-no-targets = На этом секторе не чувствуется добычи.

predator-sense-title = Чутьё хищника
vampire-locate-search-placeholder = Поиск...

vampiric-claws-remove-popup = Вы убираете когти.

# Umbrae abilities
action-vampire-cloak-of-darkness-start = Вы растворяетесь в тенях!
action-vampire-cloak-of-darkness-stop = Вы выходите из теней.

action-vampire-shadow-snare-placed = Вы установили теневой силок.
action-vampire-shadow-snare-wrong-place = Сюда нельзя ставить ловушку.
action-vampire-shadow-snare-scatter = Вы разбросали теневую ловушку.
vampire-shadow-snare-oldest-removed = Ваш старый теневой силок рассеивается.
ent-shadow-snare-ensnare = теневой силок

action-vampire-shadow-anchor-returned = Вы вернулись к теневому якорю
action-vampire-shadow-anchor-installed = Вы закрепили место в тенях

action-vampire-shadow-boxing-start = Вы начинаете теневой бой.
action-vampire-shadow-boxing-stop = Теневой бой остановлен.
action-vampire-shadow-boxing-ends = Теневой бой заканчивается.

action-vampire-dark-passage-wrong-place = Тьма здесь непроницаема...
action-vampire-dark-passage-activated = Вы проскользнули сквозь тьму...

action-vampire-extinguish-activated = Вы поглотили свет вокруг себя...({$count})

action-vampire-eternal-darkness-not-enough-blood = У вас кончилась кровь для поддержания вечной тьмы.
action-vampire-eternal-darkness-start = Вы призвали вечную тьму...
action-vampire-eternal-darkness-stop = Вечная тьма рассеялась...

# Dantalion
vampire-enthrall-start = Вы проникаете в разум {CAPITALIZE(THE($target))}...
vampire-enthrall-success = {CAPITALIZE(THE($target))} преклоняет колено и становится вашим траллом.
vampire-enthrall-target = Ваш разум подавлен вампирским доминированием!
vampire-enthrall-limit = Вы не можете контролировать больше траллов.
vampire-enthrall-invalid = Эта цель не может быть порабощена.
vampire-thrall-released = Вампирская хватка над вами слабеет.

vampire-pacify-invalid = Эта цель не может быть умиротворена.
vampire-pacify-success = {CAPITALIZE(THE($target))} поддаётся вашему сокрушительному спокойствию.
vampire-pacify-target = Сокрушительное спокойствие топит вашу волю к борьбе!

vampire-subspace-swap-thrall = Вы не можете меняться местами через подпространство со своими траллами.
vampire-subspace-swap-dead = Этот разум вне вашей досягаемости.
vampire-subspace-swap-failed = Подпространственный разлом бесполезно гаснет.
vampire-subspace-swap-success = Пространство скручивается, когда вы меняетесь местами с {CAPITALIZE(THE($target))}!
vampire-subspace-swap-target = Реальность искривляется, и вас разрывает в новую позицию!

vampire-rally-thralls-success = {$count ->
    [one] Ваш зов возвращает тралла на вашу сторону!
    *[other] Ваш зов возвращает {$count} траллов на вашу сторону!
}
vampire-rally-thralls-none = Ни один из ваших траллов не может ответить на зов.
vampire-thrall-holy-water-freed = Святая вода очищает ваш разум от вампирской хватки!

vampire-blood-bond-start = Реки крови связывают вас с вашими траллами.
vampire-blood-bond-stop = Вы ослабляете кровную связь.
vampire-blood-bond-no-thralls = У вас нет порабощённых слуг для связи.
vampire-blood-bond-stop-blood = Связь разрывает себя; вам не хватает крови чтобы поддерживать её.

action-vampire-not-enough-power = Вашей силы недостаточно (нужно >1000 общей крови и 8 уникальных жертв).

# Gargantua
vampire-blood-swell-start = Ваши мышцы набухают от нечестивой силы
vampire-blood-swell-end = Кровавая ярость утихает.

vampire-blood-rush-start = Кровь бурлит в ваших конечностях!
vampire-blood-rush-end = Ваша сверхъестественная скорость угасает.

vampire-seismic-stomp-activate = Земля содрогается под вашей яростью!

vampire-overwhelming-force-start = Ваше присутствие становится неподвижным.
vampire-overwhelming-force-stop = Вы ослабляете железную хватку.
vampire-overwhelming-force-too-heavy = Этот объект слишком тяжёлый, чтобы сдвинуть!
vampire-overwhelming-force-door-pried = Вы вырываете дверь грубой силой.

vampire-demonic-grasp-hit = Демонический коготь хватает вас!
vampire-demonic-grasp-pull = Коготь тащит вас к вампиру!

vampire-charge-start = Вы несётесь вперёд с неудержимой силой!
vampire-charge-impact = Вы врезаетесь в {CAPITALIZE(THE($target))} с сокрушительной силой!


vampire-blood-swell-cancel-shoot = Ваши пальцы не влезают в спусковую скобу!!

vampire-holy-place-burn = Священная земля обжигает вашу нечестивую плоть!

alerts-vampire-blood-swell-name = Прилив крови
alerts-vampire-blood-swell-desc = Ваши мышцы наливаются нечестивой силой.
alerts-vampire-blood-rush-name = Кровавый рывок
alerts-vampire-blood-rush-desc = Сверхъестественная скорость струится по вашим конечностям.

Vamp-converted-title = Порабощён!
Vamp-converted-text =
    Вы были порабощены!
    Верно подчиняйтесь своему хозяину, вы можете получить доступ к хайвмайнду через '+p'
Vamp-converted-confirm = Понятно

vampire-overwhelming-force-latch-blocked = Вы не можете ухватиться за нечто столь неподвижное!

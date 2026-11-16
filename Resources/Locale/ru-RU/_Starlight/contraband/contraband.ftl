contraband-type-syndicate = Синдикат
contraband-type-magical = магическая
contraband-type-ussp = СССП
contraband-type-tsf = Транс-Солнечная Федерация
contraband-type-cybernetic = кибернетическая
contraband-type-medtak = МедТак
contraband-type-casino = Казино Гаморра
contraband-type-cosmic = космический культ
contraband-type-central-command = Центральное командование
contraband-type-nanotrasen = NanoTrasen
contraband-type-eto = ОТО

contraband-authorization-prescription-patients = пациенты с действующим рецептом
contraband-examine-text-restricted =
    { $itemType ->
        *[item] [color=yellow]Этот предмет разрешён только {$authorizations}.[/color]
        [reagent] [color=yellow]Этот реагент разрешён только {$authorizations}.[/color]
    }

contraband-examine-text-tier =
    { $itemType ->
        *[item] [color={$color}]Это предмет контрабанды категории «{$descriptor}», уровень {$tier}.[/color]
        [reagent] [color={$color}]Это реагент контрабанды категории «{$descriptor}», уровень {$tier}.[/color]
    }

contraband-examine-text-tier-typed =
    { $itemType ->
        *[item] [color={$color}]Это предмет контрабанды типа «{$contrabandType}» категории «{$descriptor}», уровень {$tier}.[/color]
        [reagent] [color={$color}]Это реагент контрабанды типа «{$contrabandType}» категории «{$descriptor}», уровень {$tier}.[/color]
    }

contraband-tier-1 = ограниченный
contraband-tier-2 = строго регулируемый
contraband-tier-3 = серьёзный
contraband-tier-4 = особо незаконный
contraband-tier-5 = чрезвычайно опасный

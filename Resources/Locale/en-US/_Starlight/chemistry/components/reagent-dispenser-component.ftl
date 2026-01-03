reagent-dispenser-popup-no-energy = Not enough energy!
reagent-dispenser-component-cannot-fit-message = The container can't hold that much!

# ChemMaster linking
reagent-dispenser-chemmaster-toggle-on = Dispense to: ChemMaster
reagent-dispenser-chemmaster-toggle-off = Dispense to: Container
reagent-dispenser-chemmaster-linked = Linked: {$name}
reagent-dispenser-chemmaster-not-linked = Not linked
reagent-dispenser-chemmaster-nearby-label = Nearby ChemMasters
reagent-dispenser-chemmaster-none-nearby = No ChemMasters nearby
reagent-dispenser-chemmaster-out-of-range = ChemMaster out of range!
reagent-dispenser-chemmaster-itemlist-entry = {$linked ->
    [true] {"[Linked] "}
    *[false] {""}
} {$name} ({$beacon}) {$inRange ->
    [true] {""}
    *[false] (Out of Range)
}
reagent-dispenser-chemmaster-select-button = Linked Devices
reagent-dispenser-chemmaster-select-title = Linked Devices
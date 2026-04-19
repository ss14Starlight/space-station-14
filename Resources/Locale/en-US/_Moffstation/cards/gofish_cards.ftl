gofish-card-name-reverse = gofish card
gofish-card-desc-reverse = You can't tell what is on the other side of that fish card.

gofish-card-name = { gofish-card-value-name } Card
gofish-card-value-name = { $card ->
    [rules] Rules
    [carp] Space Carp
    [magic] Magic Carp
    [holo] Holocarp
    [rainbowcarp] Rainbow Carp
    [acmeco] AcmeCo
    [dromedaryco] DromedaryCo
    [nomads] Nomads
    [spessman] Spessman
    [ian] Ian
    [lisa] Lisa
    [puppy] Puppy Ian
    [oldian] Old Ian
    [appledonut] Apple Donut
    [bungodonut] Bungo Donut
    [chocolatedonut] Chocolate Donut
    [pinkdonut] Pink Donut
    [ertengineer] ERT Engineer
    [ertleader] ERT Leader
    [ertmedic] ERT Medic
    [ertsecurity] ERT Security
    [bingus] Bingus
    [exception] Exception
    [floppa] Floppa
    [runtime] Runtime
    [apple] Apple
    [banana] Banana
    [grapes] Grapes
    [orange] Orange
    [brown] Brown Mouse
    [grey] Grey Mouse
    [real] Real Mouse
    [white] White Mouse
    [deathshead] Deathshead Mothroach
    [moproach] Moproach
    [mothroach] Regular Mothroach
    [rosy] Rosy Mothroach
    [nukieelite] Elite Nukie
    [nukiejuggernaut] Nukie Juggernaut
    [nukiemedic] Nukie Medic 
    [nukieoperative] Nukie Operative
    [drazil] Drazil Plushie
    [lizard] Lizard Plushie
    [rainbowlizard] Rainbow Lizard Plushie
    [spacelizard] Space Lizard Plushie
    [fourteenloko] Fourteen Loko
    [grape] Grape Soda
    [smitecranberry] Smite Cranberry Soda
    [spacecola] Space Cola
    [bloodbag] Blood Bag
    [bruisepack] Bruise Pack
    [gauze] Gauze
    [ointment] Ointment
    [clown] Clown
    [mime] Mime
    [passenger] Passenger
    [skeleton] Skeleton
   *[other] {$card}
}

gofish-card-desc = 
    The border of this card is { $suit }.
    It belongs to the { gofish-card-group-name } group of cards!

gofish-card-suit-name = { $suit ->
    [gofishblue] Blue
    [gofishgreen] Green
    [gofishred] Red
    [gofishyellow] Yellow
   *[other] {$suit}   
}

gofish-card-group-name = { $id ->
    [carp] Carp
    [magic] Carp
    [holo] Carp
    [rainbowcarp] Carp
    [acmeco] Cigarette
    [dromedaryco] Cigarette
    [nomads] Cigarette
    [spessman] Cigarette
    [ian] Corgi
    [lisa] Corgi
    [puppy] Corgi
    [oldian] Corgi
    [appledonut] Donut
    [bungodonut] Donut
    [chocolatedonut] Donut
    [pinkdonut] Donut
    [ertengineer] ERT
    [ertleader] ERT
    [ertmedic] ERT
    [ertsecurity] ERT
    [bingus] Cat
    [exception] Cat
    [floppa] Cat
    [runtime] Cat
    [apple] Fruit
    [banana] Fruit
    [grapes] Fruit
    [orange] Fruit
    [brown] Mice
    [grey] Mice
    [real] Mice
    [white] Mice
    [deathshead] Mothroach
    [moproach] Mothroach
    [mothroach] Mothroach
    [rosy] Mothroach
    [nukieelite] Nukie
    [nukiejuggernaut] Nukie
    [nukiemedic] Nukie
    [nukieoperative] Nukie
    [drazil] Plushie
    [lizard] Plushie
    [rainbowlizard] Plushie
    [spacelizard] Plushie
    [fourteenloko] Soda
    [grape] Soda
    [smitecranberry] Soda
    [spacecola] Soda
    [bloodbag] Topical
    [bruisepack] Topical
    [gauze] Topical
    [ointment] Topical
    [clown] Troublemaker
    [mime] Troublemaker
    [passenger] Troublemaker
    [skeleton] Troublemaker
   *[other] !!Brother you should not be seeing this...!!
}

gofish-card-rules-content = [color=#1b67a5]
                                                  {"[head=1]                  Go Fish![/head]"}
                                                  {"[head=4]               Card Game Rules & How to Play[/head]"}[/color]

    ════════════════════════════════════════

    {"[head=2]    Introduction:[/head]"}

    Go Fish is a classic card game designed to be played by 2-6 players. The goal of Go Fish! is to collect all four cards from the same group to score points.
    To start a game of Go Fish, begin by shuffling the deck. The amount of cards you would then deal depends on the number of players...

    ════════════════════════════════════════

    {"[head=2]    Setup:[/head]"}

     • {"[bold]2-3 players:[/bold]"} Deal 7 cards each.
     • {"[bold]4-6 players:[/bold]"} Deal 5 cards each.
    Once the cards have been dealt, return the deck to the middle of the table face down.

    ════════════════════════════════════════

    {"[head=2]    Gameplay:[/head]"}

    Players will take turn asking another player if they have a card belonging to a specific group. The asking player may only asks for cards from a specific group, if they hold one of those cards in their hand.
        {"[bold]        Player 1:[/bold]"} Hey Player 2, got any Mothroaches?
    Player 2 must surrender all Mothroach cards to Player 1 if they have any in their possesion. If this is the case, Player 1 can continue their turn and ask another player, repeating until they get one wrong.
        {"[bold]        Player 1:[/bold]"} Hey Player 3, got any Mothroaches?
    If the player doesn't have the a card belonging to that group, they must respond with "Go Fish!", forcing the player to draw a card.
        {"[bold]                                                  Player 2:[/bold]"} Nope! Go Fish!
    Player 1 would then draw a card from the deck. If they manage to draw a card belonging to a group they just asked for, then they must announce that and continue their turn.
        {"[bold]        Player 1:[/bold]"} I drew a Mothroach! I get to go again!
    If the card does not match the group they asked for, then the next player in the turn order begins their turn.

    ════════════════════════════════════════

    {"[head=2]    How To Win:[/head]"}

    If a player manages to collect all four cards belonging to the same group, the player must place the four cards face up on the table and announce it to the other players.
    For every set a player collects, they score one point.

    The game ends when there are no cards left in the deck and all groups have been united. The player who has the most points will be declared the winner!

    ════════════════════════════════════════

    {"[head=2]    Tips for Playing Go Fish![/head]"}

    There are 13 groups in a standard Go Fish! deck.
    Each group has four cards, with each card having a border color of either {"[bold][color=Red]Red[/color][/bold], [bold][color=DodgerBlue]Blue[/color][/bold], [bold][color=LimeGreen]Green[/color][/bold] or [bold][color=GoldenRod]Yellow[/color][/bold]"}.
    
    The 13 groups are as follows...
    {"[mono][bold]1:[/bold] Card         [bold]6:[/bold] Cats       [bold]11:[/bold] Plushies"}
    {"[bold]2:[/bold] Cigarettes   [bold]7:[/bold] Fruit      [bold]12:[/bold] Soda"}
    {"[bold]3:[/bold] Corgis       [bold]8:[/bold] Mice       [bold]13:[/bold] Troublemakers"}
    {"[bold]4:[/bold] Donuts       [bold]9:[/bold] Mothroachs"}
    {"[bold]5:[/bold] ERT         [bold]10:[/bold] Nukies"}[/mono]

    {"  • Remember to [bold]pay attention[/bold] to what other players ask for!"}

    {"  • [bold]Remember who has which cards[/bold] so your guesses can be"}
        more successful!

    {"  • [bold]Avoid exposing cards[/bold] which you are a close to completing"}
        a set with!

    {"  • [bold]Remember to have fun![/bold]"}

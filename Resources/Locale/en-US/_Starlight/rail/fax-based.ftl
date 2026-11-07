rr-criminal = Criminal
rr-criminal-desc = That was a mistake, at any moment I could be reported and the hunt would begin.

rr-criminal-message = Well-wisher: We have just intercepted a fax, you’ve been declared wanted. Nevertheless, your recent malicious act against NT has greatly impressed us. We’ll send you a gift by mail, it should arrive in about 20 minutes.
rr-criminal-wrapped-message = [bold]Well-wisher[/bold]: We have just intercepted a fax, you’ve been declared wanted. Nevertheless, your recent malicious act against NT has greatly impressed us. We’ll send you a gift by mail, it should arrive in about 20 minutes.

rr-criminal-letter-name = ORDER, dangerous criminal {$subject} is wanted
rr-criminal-1-letter-content =
                 [head=3]NT Information Security Department[/head]
    ─────────────────────────────────────────
                      [center][color=#006666][italic][bold]Order for Immediate Interception[/bold][/italic][/color][/center]
    ─────────────────────────────────────────
    A dangerous criminal named '{$subject}' has infiltrated your station.

    {CAPITALIZE(SUBJECT($subject))} {CONJUGATE-BE($subject)} charged with multiple counts of █████████████, as well as ██████████████. There is also clear evidence pointing to crimes of a ██████████████ nature against █████████████. {CAPITALIZE(SUBJECT($subject))} may be armed.

    In accordance with Directive 25, Section B of the Code of Information Crimes, you are ordered to deliver {OBJECT($subject)} to Central Command, dead or alive.

    {$subject} [italic]must[/italic] be:
    - [bold]Handcuffed[/bold] (even if {SUBJECT($subject)} {CONJUGATE-BE($subject)} compliant)
    - [bold]Searched[/bold]
    - [bold]Interrogated[/bold]

    Failure to comply with this order may result in charges of incompetence and dismissal.
    ─────────────────────────────────────────
   ⠀                                    [italic]Place for stamps[/italic]

rr-criminal-failed-name = Decision regarding the criminal {$subject}
rr-criminal-failed-content =
                         [head=3]Authorized Inspector on the Case[/head]
    ─────────────────────────────────────────
                                      [center][color=#006666][italic][bold]Mitigation Decision[/bold][/italic][/color][/center]
    ─────────────────────────────────────────
    Regarding the case of the criminal {$subject}. We have just intercepted a recruitment letter indicating that the crimes committed were not of direct intent.

    In this regard, the crime is no longer classified as informational extremism, but as fatal negligence.

    You should [bold]interrogate[/bold] the criminal, and if {SUBJECT($subject)} admits {POSS-ADJ($subject)} guilt, [bold]parole[/bold] {OBJECT($subject)} and allow {OBJECT($subject)} to work it off in lower positions, with damages assessed from one and a half to ten million credits.
    ─────────────────────────────────────────
   ⠀                                    [italic]Place for stamps[/italic]

rr-nt-isd = NT ISD
rr-cc = CentComm

#Bounty

rr-bounty = Bounty
rr-bounty-desc = Taking that job may have pissed off the wrong people...

rr-bounty-message = [italic]They promised as long as I dont get caught anytime soon for what I did, they'll wire the payment for the job, just need to stay on my toes and keep calm.[/italic]
rr-bounty-wrapped-message = [italic]They promised as long as I don't get caught anytime soon for what I did, they'll wire the payment for the job, just need to stay on my toes and keep calm.[/italic]

rr-bounty-letter-name = {$subject} Bounty
rr-bounty-1-letter-content =
                   [head=3]ITG Inter-Corporate Affairs Office[/head]
    ─────────────────────────────────────────
                                 [head=2][center][color=#fd3f3f][bold]Bounty Contract[/bold][/color][/center][/head]
    ─────────────────────────────────────────
    By Order of Boardmember █████████████

    {$subject} has been issued as an ITG bounty to the sum of 1200 salvage tickets. {CAPITALIZE(SUBJECT($subject))} is considered a legal target for retributive justice for the crime of █████ █████ in accordance with NT-CC signed Inter-Corporate charters, but is not to be considered criminally liable for local corporate law

    ITG operators are ordered to keep {OBJECT($subject)} alive, but may use force if necessary to detain the subject. Local Security Forces are expected to prevent unnecessary casualties, and may provide material assistance or intelligence, but are otherwise expected to not interfere to limit legal liability.

    {$subject} [italic]must[/italic] be:
    - [bold]Immediatly Handcuffed[/bold] (even if {SUBJECT($subject)} {CONJUGATE-BE($subject)} compliant)
    - [bold]Interrogated aboard the ATS[/bold]

    Legal Procedures that may close the bounty are expected to take effect within 30 minutes of issuance
    ─────────────────────────────────────────
   ⠀                                    [italic]Place for stamps[/italic]

rr-bounty-failed-name = Succesful Retrieval of {$subject} Assets
rr-bounty-failed-content =
                     [head=3]ITG Inter-Corporate Affairs Office[/head]
    ─────────────────────────────────────────
                                   [head=2][center][color=#fd3f3f][bold]Bounty Payment[/bold][/color][/center][/head]
    ─────────────────────────────────────────
    Regarding the case of {$subject}. We have received compensation for their violations from a secure account.

    {$subject} is to be relieved of valuable goods and released.


    This form may be slotted into your ticket vendor for payment.
    ─────────────────────────────────────────
   ⠀                                    [italic]Place for stamps[/italic]

rr-bounty-completed-name = Corporate Injunction on the Case of {$subject}
rr-bounty-completed-content =
                     [head=3]ITG Inter-Corporate Affairs Office[/head]
    ─────────────────────────────────────────
                                [head=2][center][color=#fd3f3f][bold]Bounty Nullification[/bold][/color][/center][/head]
    ─────────────────────────────────────────
    Regarding the case of {$subject}. The bounty order has been frozen due to a corporate injunction.

    The bounty for {$subject} is to be considered null until further notice.


    Due to the nullifcation, no payment will be issued.
    ─────────────────────────────────────────
   ⠀                                    [italic]Place for stamps[/italic]

rr-itg = Inter-Stellar Trade Guild

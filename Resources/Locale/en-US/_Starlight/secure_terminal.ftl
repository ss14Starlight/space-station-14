## Secure Command Terminal – UI strings

secure-terminal-window-title = Secure Terminal
secure-terminal-requests-header = Requests
secure-terminal-information-header = Information
secure-terminal-authorization-header = Authorization

secure-terminal-select-request = Select a request from the list on the left to see details.

secure-terminal-request-button = Request
secure-terminal-request-button-confirm = Confirm?
secure-terminal-authorize-button = Authorize
secure-terminal-deny-button = Deny / Cancel
secure-terminal-recall-button = Recall Armory
secure-terminal-recall-locked = { $minutes ->
    [1] Recall available in 1 minute.
   *[other] Recall available in {$minutes} minutes.
}
secure-terminal-used-note = This armory has been permanently activated or recalled this round and cannot be deployed again.
secure-terminal-already-used = This resource has already been used this round and cannot be requested again.

secure-terminal-auth-waiting = No active proposal for this request.
secure-terminal-auth-desc = Current proposal — no response = [color=red]red[/color], agreed = [color=green]green[/color]:
secure-terminal-awaiting-member = Awaiting {$label}

secure-terminal-pending-countdown-label = Expires in {$minutes}m {$seconds}s…
secure-terminal-countdown-label = Activating in {$minutes}m {$seconds}s…

secure-terminal-fee-note = Processing fee: {$fee}
secure-terminal-salary-note = Station salary reduced by {$penalty}% due to mobilization cost.
secure-terminal-delay-note = { $minutes ->
    [1] ETA: 1 minute after authorization.
   *[other] ETA: {$minutes} minutes after authorization.
}

secure-terminal-requires-no-war-note = Disabled during War Ops.
secure-terminal-requires-war-note = Only available during War Ops.
secure-terminal-requires-alert-note = Requires {$level} alert to be active.
secure-terminal-alert-time-remaining = { $minutes ->
    [1] Alert must be active for 1 more minute before this can be requested.
   *[other] Alert must be active for {$minutes} more minutes before this can be requested.
}
secure-terminal-on-cooldown-note = { $minutes ->
    [1] On cooldown — available in 1 minute.
   *[other] On cooldown — available in {$minutes} minutes.
}
secure-terminal-requires-alert-suffix = Need: {$level}
secure-terminal-requires-war-suffix = Need: War Ops

secure-terminal-reason = Insert request reason:

## Server → global announcements

secure-terminal-proposal-created = {$request} has been requested and is awaiting co-authorization.
secure-terminal-proposal-created-reason = {$request} has been requested and is awaiting co-authorization. Reason: {$reason}
secure-terminal-proposal-denied = {$request} request has been cancelled.
secure-terminal-proposal-denied-cc = {$request} request has been denied by Central Command.
secure-terminal-radio-proposal = {$request} has been proposed. Please go to the nearest Keycard Authentication Device to authorize or deny.
secure-terminal-radio-proposal-reason = {$request} has been proposed. Please go to the nearest Keycard Authentication Device to authorize or deny. Reason: {$reason}
secure-terminal-radio-denied = {$request} request has been cancelled.
secure-terminal-activation-countdown = {$request} has been fully authorized.
    Activating in {$minutes} minutes.
    Station salary has been reduced due to the mobilization cost.
secure-terminal-unknown-job = Unknown

## Popup messages

secure-terminal-no-station = No station found for this console.
secure-terminal-request-denied = Access denied.
secure-terminal-authorize-denied = You do not hold the required clearance to co-sign this request.
secure-terminal-requires-war = This request is only available when War Ops have been formally declared.
secure-terminal-wrong-alert = The current alert level does not meet this request's requirements.
secure-terminal-alert-not-long-enough = The alert level has not been active long enough to authorize this. Please wait and try again.
secure-terminal-recall-too-soon = The armory has not been deployed long enough to recall. Please wait.
secure-terminal-on-cooldown = This request is on cooldown.
secure-terminal-already-pending = A proposal for this request is already pending.
secure-terminal-already-active = Another request is already pending or activating. Wait for it to complete before making a new one.
secure-terminal-no-active-proposal = No active proposal found for this request.
secure-terminal-already-authorized = You have already authorized this proposal.
secure-terminal-already-activated = This terminal already authorized this proposal.
secure-terminal-auth-note = This terminal is only for authorization.
secure-terminal-authorized-by = Attention — {$request} request has been authorized. Authorized by: {$signatories}.
secure-terminal-armory-recalled = {$request} recall order issued. Armory deployment has been cancelled.
secure-terminal-awaiting-admin = Attention — {$request} request has been sent. Awaiting authorization by Central Command.
secure-terminal-admin = Requesting Admin Approval for: {$request}
                        Reason: {$reason}
                        Use the AGhost to Approve/Deny the request.

## Request names & descriptions

secure-terminal-warops-security-name = Nuclear Response Team
secure-terminal-warops-security-desc = Deploys an ERT Security detail specialized for War Ops. Only available during War Ops.
                                       Use when the station is under direct armed assault during a declared War Ops.
secure-terminal-warops-security-announcement = An Emergency Response Team — Security Specialized detail — has been authorized and is en route. Estimated arrival: 30 minutes.

secure-terminal-ert-security-name = ERT Security
secure-terminal-ert-security-desc = Deploys an ERT Security detail.
secure-terminal-ert-security-announcement = An Emergency Response Team — Security detail — has been authorized and is en route. Estimated arrival: 10 minutes.

secure-terminal-ert-engineering-name = ERT Engineering
secure-terminal-ert-engineering-desc = Deploys an ERT Engineering detail to assist with critical station infrastructure.
    Recommended when the station has suffered catastrophic structural, atmospheric, or power failures beyond local repair capacity.
secure-terminal-ert-engineering-announcement = An Emergency Response Team — Engineering detail — has been authorized and is en route. Estimated arrival: 10 minutes.

secure-terminal-ert-medical-name = ERT Medical
secure-terminal-ert-medical-desc = Deploys an ERT Medical detail for mass casualty triage and emergency surgery.
    Recommended when the station's medical department is overwhelmed, incapacitated, or destroyed.
secure-terminal-ert-medical-announcement = An Emergency Response Team — Medical detail — has been authorized and is en route. Estimated arrival: 10 minutes.

secure-terminal-ert-janitorial-name = ERT Janitorial
secure-terminal-ert-janitorial-desc = Deploys an ERT Janitorial detail for hazardous cleanup and station restoration.
    Recommended following large-scale biological, chemical, or environmental contamination requiring rapid decontamination.
secure-terminal-ert-janitorial-announcement = An Emergency Response Team — Janitorial detail — has been authorized and is en route. Estimated arrival: 10 minutes.

secure-terminal-ert-chaplain-name = ERT Chaplain
secure-terminal-ert-chaplain-desc = Deploys an ERT Chaplain for crew morale and last rites support.
    Provides pastoral support and maintains crew morale during prolonged emergencies.
secure-terminal-ert-chaplain-announcement = An Emergency Response Team — Chaplaincy — has been authorized and is en route. Estimated arrival: 10 minutes.

secure-terminal-ert-cburn-name = ERT CBURN
secure-terminal-ert-cburn-desc = Deploys an ERT CBURN detail.
secure-terminal-ert-cburn-announcement = An Emergency Response Team — CBURN detail — has been authorized and is en route. Estimated arrival: 15 minutes.

secure-terminal-code-gamma-name = Code GAMMA
secure-terminal-code-gamma-desc = Escalates the station to [color=palevioletred]GAMMA[/color] alert. Martial law — all civilians are to be escorted by security to safe areas.
    Security must be armed at all times. All civilians must report to their nearest head of staff and be escorted to a secure location. Emergency lights activate.
secure-terminal-code-gamma-announcement = Attention! Code GAMMA is being put into effect shortly. Martial law will be enforced. All crew report to your nearest head of staff immediately.

secure-terminal-end-gamma-name = End GAMMA Alert
secure-terminal-end-gamma-desc = Lifts [color=palevioletred]GAMMA[/color] alert and returns the station to Green. Requires GAMMA to have been active for at least 15 minutes.
secure-terminal-end-gamma-announcement = Code GAMMA is being lifted. The station is being restored to normal operations. Remain alert and await further instruction from your head of staff.

secure-terminal-code-psi-name = Code PSI
secure-terminal-code-psi-desc = Escalates the station to [color=mediumpurple]PSI[/color] alert. Hostile synthetic units detected — avoid non-conforming cyborgs and seek command staff.
    Indicates hostile or non-conforming cyborg activity. All crew must avoid unknown borgs, stay in groups, and seek head-of-staff guidance.
secure-terminal-code-psi-announcement = Attention! Command has authorized Code PSI. Non-NanoTrasen silicon units have been identified as an active threat. All crew — report to your nearest head of staff.

secure-terminal-end-psi-name = End PSI Alert
secure-terminal-end-psi-desc = Lifts [color=mediumpurple]PSI[/color] alert and returns the station to Green. Requires PSI to have been active for at least 15 minutes.
secure-terminal-end-psi-announcement = Code PSI is being lifted. The identified synthetic threat has been neutralized. The station is returning to normal operations.

secure-terminal-armory-gamma-name = Gamma Armory
secure-terminal-armory-gamma-desc = Dispatches the [color=palevioletred]Gamma Armory[/color] — heavy weapons cache for GAMMA situations. One-time deployment.
                                    Issues heavy-duty security equipment to authorized personnel.
secure-terminal-armory-gamma-announcement = The Gamma Armory has been authorized and is en route.

secure-terminal-armory-psi-name = Psi Armory
secure-terminal-armory-psi-desc = Dispatches the [color=mediumpurple]Psi Armory[/color] — anti-cybernetic weaponry for PSI situations. One-time deployment.
                                  Provides tools needed to neutralize non-conforming silicons.
secure-terminal-armory-psi-announcement = The Psi Armory has been authorized and is en route.

secure-terminal-med-pod-name = Emergency Medical Pod
secure-terminal-med-pod-desc = Dispatches the Emergency Medical Pod — rapid-deployment triage with surgical and revival equipment.
    Use when mass casualties exceed the station's medical capacity.
secure-terminal-med-pod-announcement = The Emergency Medical Pod has been authorized and is en route. Estimated arrival: 5 minutes.

secure-terminal-nukerequest-name = Self Destruct Code
secure-terminal-nukerequest-desc = Request the nuclear self-destruct codes.
                                   Misuse of the nuclear request system will not be tolerated under any circumstances.
                                   Transmission does not guarantee a response.

secure-terminal-code-violet-name = Code Violet
secure-terminal-code-violet-desc = Escalates the station to [color=Violet]Violet[/color] alert.

secure-terminal-end-violet-name = End Violet Alert
secure-terminal-end-violet-desc = Lifts [color=Violet]Violet[/color] alert and returns the station to Green. Requires Violet to have been active for at least 10 minutes.

secure-terminal-emergency-maintenance-name = Emergency Maintenance Access
secure-terminal-emergency-maintenance-desc = Grant Emergency Maintenance Access.
secure-terminal-emergency-maintenance-announcement = Access restrictions on maintenance and external airlocks have been removed.

secure-terminal-end-emergency-maintenance-name = Revoke Emergency Maintenance Access
secure-terminal-end-emergency-maintenance-desc = Revoke Emergency Maintenance Access.
secure-terminal-end-emergency-maintenance-announcement = Access restrictions on maintenance and external airlocks have been re-added.

secure-terminal-emergency-station-name = Station-Wide Emergency Access
secure-terminal-emergency-station-desc = Activate Station-Wide Emergency Access.
secure-terminal-emergency-station-announcement = Access restrictions on all station airlocks have been removed due to an ongoing crisis. Trespassing laws still apply unless ordered otherwise by Command staff.

secure-terminal-end-emergency-station-name = Deactivate Station-Wide Emergency Access
secure-terminal-end-emergency-station-desc = Deactivate Station-Wide Emergency Access.
secure-terminal-end-emergency-station-announcement = Access restrictions on all station airlocks have been re-added. Seek station AI or a colleague's assistance if you are stuck.

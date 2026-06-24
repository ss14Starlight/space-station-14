command-description-radio-addcustom =
    Add a custom channel to the specified component on the piped entity. Specify true or false at the end to ensure the component exists.
command-description-radio-remcustom =
    Remove a custom channel with the given ID from the specified component on the piped entity.
command-description-container-insertentity =
    Inserts the given entity into the specified container on the piped entity.
command-description-container-insert =
    Inserts the piped entities into the specified container on the specified entity.
command-description-container-create =
    Creates a new container on the piped entity.
command-description-container-createslot =
    Creates a new containerslot on the piped entity.
command-description-container-delete =
    Deletes a container on the piped entity.
command-description-container-drop =
    Drops all contained entities from the specified container on the piped entity.
command-description-container-dropandget =
    Drops all contained entities from the specified container on the piped entity, and return all dropped items instead of the piped entity.
command-description-container-dropanddelete =
    Drops all contained entities from the specified container on the piped entity, then delete the container.
command-description-container-get =
    Gets the container object of the given container ID on the piped entity.
command-description-container-getentities =
    Gets all entities in the given container on the piped entity.
command-description-container-getcontaining =
    Gets all containers currently containing the piped entity.
command-description-container-getoutercontainer =
    Gets the outermost container that is containing the piped entity.
command-description-container-getowner =
    Gets the entity that owns the specified container.
command-description-solution-adjcapacity =
    Adjusts the capacity on the given solution.
command-description-solution-adjtemperature =
    Adjusts the capacity on the given solution.
command-description-solution-adjthermalenergy =
    Adjusts the capacity on the given solution.
command-description-solution-create=
    Creates a new solution with a given name on the piped entity. Returns the existing solution if it exists already.
command-description-solution-delete=
    Deletes the specified solution on the piped entity.
### Starlight (upstream #39080)
command-description-subtlemessage =
    Sends a subtle message to all the input entities.
command-description-grid-getplayers =
    Gets all players on the piped grid(s)
command-description-grid-get =
    Gets the grid(s) the piped player(s) are standing on.
command-description-grid-getstation =
    Gets the station(s) that the piped player(s) are standing on, or that of the entity itself if the grid is piped in.
command-description-crewmanifest-addto =
    Adds the piped entity to the specified station's crew manifest.
command-description-crewmanifest-removefrom =
    Removes the piped entity from the specified station's crew manifest.
command-description-crewmanifest-addplayer =
    Adds the specified player to the crew manifest(s) of the piped station(s).
command-description-crewmanifest-removeplayer =
    Removes the specified player to the crew manifest(s) of the piped station(s).
command-description-storage-reshape =
    Reshapes the storage based off data given via box2iconstructor command.
command-description-box2iconstructor-new =
    Create a new Box2i list definition on the entity, chain together with box2iconstructor:add commands then follow up with a command that requires it.
command-description-box2iconstructor-add =
    Add a new Box2i to the existing definition. Call box2iconstructor:new before using this.
command-description-box2iconstructor-clean =
    Clean up an unused Box2i list definition on the entity.
command-description-vector2dataconstructor-new =
    Create a new Vector2 list definition on the entity, chain together with vector2dataconstructor:add commands then follow up with a command that requires it.
command-description-vector2dataconstructor-add =
    Add a new Vector2 to the existing definition. Call vector2dataconstructor:new before using this.
command-description-vector2dataconstructor-clean =
    Clean up an unused Vector2 list definition on the entity.
command-description-job-set =
    Changes the job of the piped entity.
command-description-job-delset =
    Changes the job of the piped entity by deleting then setting the job, so that the briefing plays.
command-description-ccomp-ensure =
    Ensures that all clients add the component with the specified name to an entity, assuming it exists.
command-description-ccomp-write =
    Attempt to make all clients vvwrite something into a client component.
command-description-ccomp-rm =
    Ensures that all clients delete the component with the specified name from an entity, assuming it exists.
command-description-globalsound-play =
    Play a sound globally for the piped entities or sessions.
command-description-polymorph-begin =
    Marker to begin a sequence of polymorph configuration instructions, will attach a PolymorphSetupComponent to the entity.
command-description-polymorph-setproto =
    Set the prototype that the entity will polymorph into.
command-description-polymorph-seteffect =
    Set a prototype to spawn on top of the polymorphed entity, typically this is used to create special effects.
command-description-polymorph-setdelay =
    Set how long in seconds must be waited before being able to activate this specific polymorph again.
command-description-polymorph-setduration =
    Set the duration the polymorph should last for in seconds before automatically reverting.
command-description-polymorph-setforced =
    Set to make so the polymorph cannot be activated or canceled by the entity itself.
command-description-polymorph-settransferdamage =
    Set to transfer the damage from the current entity to the polymorphed entity.
command-description-polymorph-settransfername =
    Set to make the polymorphed entity inherit the name of the original.
command-description-polymorph-settransferappearance =
    Set whether to transfer things like hair, skin color, height, etc. to the polymorphed entity.
command-description-polymorph-setinventory =
    Set to determine how the entity's inventory will transfer to the polymorphed entity.
command-description-polymorph-setrevertoncrit =
    Set whether to revert the polymorph when the entity enters a critical state or not.
command-description-polymorph-setrevertondeath =
    Set whether to revert the polymorph when the entity is killed or not.
command-description-polymorph-setrevertondelete =
    Set whether to revert the polymorph when the entity is deleted or not.
command-description-polymorph-setrevertoneat =
    Set whether to revert the polymorph when the entity is eaten or not.
command-description-polymorph-setallowrepeats =
    Set whether to allow repeated polymorphs or not.
command-description-polymorph-setignoreallowrepeats =
    Set to allow the polymorph to happen even if AllowRepeatedMorphs is true.
command-description-polymorph-setcooldown =
    Set the cooldown in seconds before another polymorph can take place.
command-description-polymorph-setentersound =
    Set the sound that plays when entering the polymorph.
command-description-polymorph-setexitsound =
    Set the sound that plays when exiting the polymorph.
command-description-polymorph-clearentersound =
    Clear the sound that plays when entering the polymorph.
command-description-polymorph-clearexitsound =
    Clear the sound that plays when exiting the polymorph.
command-description-polymorph-setenterpopup =
    Set the popup that appears when entering the polymorph.
command-description-polymorph-setexitpopup =
    Set the popup that appears when exiting the polymorph.
command-description-polymorph-clearcopycomp =
    Clear the list of components to copy to the polymorph.
command-description-polymorph-addcopycomp =
    Add an entry to the list of components to copy to the polymorph.
command-description-polymorph-rmcopycomp =
    Remove an entry from the list of components to copy to the polymorph.
command-description-polymorph-apply =
    Instantly apply the polymorph and finish.
command-description-polymorph-applyget =
    Instantly apply the polymorph and finish, returning the new entity.
command-description-polymorph-addaction =
    Add a polymorph action to the entity using the current polymorph setup chain. You should probably call polymorph:apply or polymorph:finish afterward.
command-description-polymorph-addactionproto =
    Add a prototyped polymorph action to the entity.
command-description-polymorph-rmaction =
    Remove a polymorph action from the entity that was added via polymorph:addaction.
command-description-polymorph-rmactionproto =
    Remove a prototyped polymorph action from the entity.
command-description-polymorph-revert =
    Revert to the previous x entity, if possible.
command-description-polymorph-reset =
    Reset the entity's polymorph to their original state.
command-description-polymorph-finish =
    Marks this polymorph setup chain as complete, cleaning up and removing the component.
command-description-vv-open =
    Open the ViewVariables window of the piped entity or path.
command-description-vv-write =
    Modify a path's value using VV (View Variables). Can use a variable for the value, but it must be a serialized string.
command-description-vv-owrite =
    Modify a path's value using VV (View Variables). Can use a raw variable for the value.
command-description-vv-read =
    Print a path's value using VV (View Variables).
command-description-vv-rsave =
    Retrieve a path's value using VV (View Variables). Can be saved to a variable.
command-description-vv-rsaveraw =
    Retrieve a path's value using VV (View Variables). Can be saved to a variable. Saves the raw value instead of the serialized string.
command-description-mind-wipe =
    Wipes a player or entity's mind. Note that this will make their game unplayable until you give them a new mind.
command-description-mind-takeover =
    Directly takeover a mob, creating a mind if it does not exist, and forcing the entity to become sentient.
command-description-mind-takeoverwipe =
    Wipe your own mind then takeover the entity. This will clear all mindroles and objectives n such.
command-description-mind-controlwipe =
    Wipe the target player's mind and make them control the piped entity, creating a new mind and making the entity sentient.
command-description-killsign-set =
    Apply a killsign to the entity using the specified state.
command-description-killsign-list =
    Lists all available killsigns.
command-description-killsign-rm =
    Remove a killsign from the entity
command-description-fixinput =
    Refreshes the input context of the entity's session.
command-description-faction-add =
    Add a faction to this entity.
command-description-faction-remove =
    Remove a faction from this entity.
command-description-faction-aggro =
    Make this entity aggessive to the target entity.
command-description-faction-deaggro =
    Make this entity no longer aggressive to the target entity.
command-description-faction-ignore =
    Make this entity and the target entity ignore each other.
command-description-faction-unignore =
    Make this entity and the target entity no longer ignore each other.
command-description-faction-clear =
    Clear this entity's factions.
command-description-npc-sethtn =
    Creates an NPC on the entity and sets it's HTN compound.
command-description-npc-setenabled =
    Enable or disable this npc's HTN behaviors.
command-description-stationinit-begin =
    Begin the process of initializing a new midround station. Attaches BecomesStationMidRoundComponent to the grid.
command-description-stationinit-setid =
    Set the ID of the station. This is to prevent duplicates.
command-description-stationinit-clearbaseprotos =
    Clear the list of base station prototypes.
command-description-stationinit-addbaseproto =
    Add a base station prototype to use.
command-description-stationinit-rmbaseproto =
    Remove a base station prototype from use.
command-description-stationinit-setallowftl =
    Set allowing anybody to FTL to the map this station resides in.
command-description-stationinit-setuseemergencyshuttle =
    Set spawning an emergency shuttle to use at round end.
command-description-stationinit-setusearmories =
    Set spawning armories that can be sent to the station with the armory command.
command-description-stationinit-setusearrivals =
    Set spawning an arrivals shuttle for this station.
command-description-stationinit-setallowdungeonspawns =
    Set allowing dungeons like the VGroid to spawn.
command-description-stationinit-setallowcargo =
    Set allowing cargo shuttles and the ATS to spawn.
command-description-stationinit-clearallowedgridspawns =
    Clear the list of gridspawns that are allowed to spawn from the base protos.
command-description-stationinit-addallowedgridspawn =
    Add a gridspawn that is allowed to spawn from the base protos.
command-description-stationinit-rmallowedgridspawn =
    Remove a gridspawn that is allowed to spawn from the base protos.
command-description-stationinit-setemergencyshuttlepath =
    Set the override to use for the emergency shuttle grid.
command-description-stationinit-clearjobs =
    Clear all jobs from this station.
command-description-stationinit-addjob =
    Add a new job to this station.
command-description-stationinit-rmjob =
    Remove a job from this station.
command-description-stationinit-setallowevents =
    Set allowing events to target this station.
command-description-stationinit-setdovariationpass =
    Set allowing the roundstart variation pass to be run on the newly created station.
command-description-stationinit-namegrid =
    Rename the target grid, the name of the grid is what will be used for the station's name when initializing.
command-description-stationinit-initialize =
    Finish setup and initialize the station.
command-description-stationinit-initializeget =
    Finish setup and initialize the station, and return the newly created station entity.
command-description-aitakeover =
    Make the piped entity take over the target AI core.
command-description-mobthreshold-initialize =
    Properly initializes a new mob threshold onto an entity.
command-description-corporeal-on =
    Makes your ghost visible and grants it the ability to speak.
command-description-corporeal-off =
    Makes your ghost invisible and revokes the ability to speak.
command-description-markup-adddesc =
    Add markup text to the piped entity's description with the given ID.
command-description-markup-editdesc =
    Edit a line of markup text from the piped entity's description with the given ID.
command-description-markup-rmdesc =
    Remove a line of markup text from the piped entity's description with the given ID.
command-description-markup-cleardesc =
    Clears all additional lines of markup text from the piped entity's description.
command-description-markup-listdesc =
    Lists all description markup texts on the piped entity and their IDs.

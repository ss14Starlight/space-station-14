using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Chemistry.EntitySystems;
using System.Linq;
using Content.Shared.Body.Organ;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Part;
using Content.Shared.NPC.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Content.Shared.Zombies;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.Language.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;
using Content.Server.Antag;
using Robust.Shared.Audio;

namespace Content.Server.Zombies
{
    public sealed partial class ZombieSystem
    {
        [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
        [Dependency] private BodySystem _body = default!;
        [Dependency] private SharedContainerSystem _containers = default!;
        [Dependency] private AntagSelectionSystem _antag = default!;

        private void NewInitialInfectedPart(EntityUid uid)
        {
            var infection = EnsureComp<BloodStreamInfectionComponent>(uid);
            infection.InfectiousBiteCount = 3;
            infection.IsInitialInfected = true;
        }

        public void UpdateInfected(TimeSpan curTime)
        {
            var infectionQuery = EntityQueryEnumerator<BloodStreamInfectionComponent, MobStateComponent, Shared.Damage.Components.DamageableComponent>();
            while (infectionQuery.MoveNext(out var uid, out var infection, out var mobState, out var damage))
            {
                if (infection.NextTickTime > curTime)
                    continue;
                infection.NextTickTime = curTime + TimeSpan.FromSeconds(1f);

                if (!HasComp<ZombieComponent>(uid))
                {

                    //Medieval bloodletting basically, drop your bloodlevel by 50%, drop the infection by the same percent. a painful, yet possible way to drop infection level
                    //inside the "not a zombie yet block" because if you have a zombified heart your blood is entirely infected
                    //more related to this in the part of this block that zombifies you


                    infection.BloodLevel = _bloodstream.GetBloodLevel(uid);


                    if (infection.BloodLevel > infection.PreviousBloodLevel)
                    {
                        infection.BloodLossRatio = infection.BloodLevel / infection.PreviousBloodLevel;
                        if (infection.BloodLossRatio < 0f)
                            infection.InfectionLevel *= infection.BloodLossRatio;
                    }
                    infection.PreviousBloodLevel = infection.BloodLevel;

                    var isDead = _mobState.IsDead(uid, mobState);
                    var isCritical = _mobState.IsCritical(uid, mobState);

                    infection.ProcChance = infection.IsInitialInfected ?
                        (isDead ? .6f : (isCritical ? .06f : 0.038f)) :
                        (isDead ? .6f : (isCritical ? 0.6f : 0.3f));

                    for (int i = 0; i < infection.InfectiousBiteCount; i++)
                    {
                        if (_random.Prob(infection.ProcChance))
                        {
                            infection.InfectionLevel += 1f;
                        }
                    }

                    if (TryComp<BloodstreamComponent>(uid, out var bloodstream)
                        && _solutionContainer.ResolveSolution(
                            uid,
                            bloodstream.BloodSolutionName,
                            ref bloodstream.BloodSolution,
                            out var bloodSolution))
                        {
                            if (bloodSolution.ContainsReagent("Ambuzol", null) && infection.MaximumSet == false)
                            {
                                infection.MaximumInfectionLevel = infection.InfectionLevel;
                                infection.MaximumSet = true;
                            }
                            else if (bloodSolution.ContainsReagent("Ambuzol", null) == false && infection.MaximumSet == true)
                            {
                                infection.MaximumInfectionLevel = 100f;
                                infection.MaximumSet = false;
                            }
                        }

                    if (infection.InfectionLevel > infection.MaximumInfectionLevel)
                    {

                        if (TryComp<BloodstreamComponent>(uid, out var bloodStream)
                            && _solutionContainer.ResolveSolution(uid, bloodStream.BloodSolutionName, ref bloodStream.BloodSolution, out var bloodStreamSolution)
                        )
                        {
                            var excessInfectionLevel = infection.InfectionLevel - infection.MaximumInfectionLevel;
                            var burnAmount = excessInfectionLevel * 0.04f;
                            var removed = bloodStreamSolution.RemoveReagent("Ambuzol", burnAmount);

                        }


                        infection.InfectionLevel = infection.MaximumInfectionLevel;

                    }

                    if (infection.InfectionLevel >= 60f)
                    {
                        var damageAmount = infection.IsInitialInfected ?
                        (_mobState.IsCritical(uid, mobState) ? 0.1f : 0.4f) :
                        (_mobState.IsCritical(uid, mobState) ? 0.1f : 0.4f);

                        if (_mobState.IsDead(uid, mobState) == false)
                        {
                            _damageable.TryChangeDamage(uid, new DamageSpecifier
                            {
                                DamageDict = new()
                                {
                                    { "Poison", damageAmount }
                                }
                            },
                            true, false);
                        }
                    }

                    if (!infection.HasBeenBriefed && infection.InfectionLevel >= 85f && infection.IsInitialInfected)
                    {
                        if (_mind.TryGetMind(uid, out var mindId, out var mind) &&
                            _player.TryGetSessionById(mind.UserId, out _))
                        {
                            _role.MindAddRole(
                                mindId,
                                "MindRoleInitialInfected",
                                mind,
                                silent: true);

                            _antag.SendBriefing(
                                uid,
                                Loc.GetString("zombie-patientzero-role-greeting"),
                                Color.Plum,
                                new SoundPathSpecifier("/Audio/Ambience/Antag/zombie_start.ogg"));

                            infection.HasBeenBriefed = true;
                            Dirty(uid, infection);
                        }


                    }

                    if (infection.InfectionLevel >= 100f)
                    {
                        var currentState = EnsureComp<PreZombificationValuesComponent>(uid);
                        if (TryComp(uid, out BloodstreamComponent? bloodstream2))
                        {
                            currentState.BeforeZombifiedBloodReagents = bloodstream2.BloodReferenceSolution;
                            currentState.BloodlossThreshold = bloodstream2.BloodlossThreshold;
                        }
                        if (TryComp<NpcFactionMemberComponent>(uid, out var factionMember))
                            currentState.OriginalFactions = factionMember.Factions.ToList();

                        ZombifyEntity(uid);
                        RemComp<PendingZombieComponent>(uid);
                        RemComp<ZombifyOnDeathComponent>(uid);
                        infection.PreviousBloodLevel = 1f;

                        if (!TryComp<BodyComponent>(uid, out var bodyPartComp))
                            return;

                        var chestPart = bodyPartComp.RootContainer.ContainedEntities.FirstOrDefault();

                        if (chestPart == EntityUid.Invalid || !TryComp<BodyPartComponent>(chestPart, out var bodyPart))
                            return;

                        var heartContainerId = SharedBodySystem.GetOrganContainerId("heart");

                        if (_containers.TryGetContainer(chestPart, heartContainerId, out var heartContainer)
                            && heartContainer.ContainedEntities.FirstOrDefault() is var oldHeart
                            && oldHeart != EntityUid.Invalid
                            && TryComp<OrganComponent>(oldHeart, out var oldHeartOrgan)
                            && _body.RemoveOrgan(oldHeart, oldHeartOrgan))
                        {
                            QueueDel(oldHeart);
                        }


                        var newHeartId = Spawn("OrganZombieHeart", Transform(chestPart).Coordinates);
                        if (TryComp<OrganComponent>(newHeartId, out var newHeartOrgan))
                        {
                            if (_body.InsertOrgan(chestPart, newHeartId, "heart", bodyPart, newHeartOrgan))
                            {
                                var ev = new SurgeryOrganImplantationCompleted(uid, chestPart, newHeartId);
                                RaiseLocalEvent(newHeartId, ref ev);
                            }
                            else
                            {
                                QueueDel(newHeartId);
                                return;
                            }
                        }
                    }
                }

                if (HasComp<ZombieComponent>(uid))
                {
                    if (!TryComp<BodyComponent>(uid, out var bodyComp))
                        return;
                    var chestPart = bodyComp.RootContainer.ContainedEntities.FirstOrDefault();
                    TryComp<BodyPartComponent>(chestPart, out var bodyPart);

                    var currentBloodLevel = _bloodstream.GetBloodLevel(uid);

                    var heartContainerId = SharedBodySystem.GetOrganContainerId("heart");
                    var hasZombieHeart = _containers.TryGetContainer(chestPart, heartContainerId, out var heartContainer)
                        && heartContainer.ContainedEntities.Any(heart =>
                            MetaData(heart).EntityPrototype?.ID == "OrganZombieHeart");
                    if (currentBloodLevel <= 0.01f && !hasZombieHeart)
                    {
                        TryComp<ZombieComponent>(uid, out var zombiecomp);
                        RemComp<BloodStreamInfectionComponent>(uid);
                        UnZombifyInPlace(uid, zombiecomp);
                    }
                }



            }
        }

        public void OnMeleeHitInfect(EntityUid uid)
        {
            var infection = EnsureComp<BloodStreamInfectionComponent>(uid);
            infection.InfectiousBiteCount += 1;
        }

        public void OnMeleeHitDeadInfect(EntityUid uid)
        {
             // If the target is dead and can be infected, infect and increment infection.
            //(unless the zombie sits there hitting it like 10 times about it wont rise immediately, if they stand there hitting it it serves the same purpose as not immediately raising so thats fine)
            //once crit it should be approx 3-5 infectious bites at 80% chance while not crit, 3 bites is 3*60% chance to increment zombification by 1, so between 1-3 infection per second
            //which is approximately 30-100s if they stop biting immediately. if they bite the dead body twice at the 3, its 5 bites at 60%, plus 20 initial, which, im not calculating just guessing, should be like, 60s max?
            //this does hurt snowballing a lot, but it can be changed to balance. it's here so that if its a firefight and someone gets crit but not zombified, they can turn once dragged back to safety in compensation for the slower snowball
            //otherwise 10 bites on crit and they zombify(probably 8-9 bites tbh)
            //so no snowballing during firefights, but prolonged further turning after
            var infection = EnsureComp<BloodStreamInfectionComponent>(uid);
                    infection.InfectiousBiteCount += 1;
                    infection.InfectionLevel += 10f;
        }

        public bool UnZombifyInPlace(EntityUid target, ZombieComponent? zombiecomp)
        {
            //For unzombifying via the exsanguinate and heart replacement method, kept separate from the normal unzombify used on cloning so i dont have to do extra to make the one function do both
            if (!Resolve(target, ref zombiecomp))
                return false;

            foreach (var (layer, info) in zombiecomp.BeforeZombifiedCustomBaseLayers)
            {
                _humanoidAppearance.SetBaseLayerColor(target, layer, info.Color);
                _humanoidAppearance.SetBaseLayerId(target, layer, info.Id);
            }
            if (TryComp<HumanoidAppearanceComponent>(target, out var appcomp))
            {
                appcomp.EyeColor = zombiecomp.BeforeZombifiedEyeColor;
            }
            _humanoidAppearance.SetSkinColor(target, zombiecomp.BeforeZombifiedSkinColor, false);
            _bloodstream.ChangeBloodReagents(target, zombiecomp.BeforeZombifiedBloodReagents);
            _language.RestoreCache((target, EnsureComp<LanguageCacheComponent>(target))); //Starlight UnZombiby fix



            if (!TryComp<PreZombificationValuesComponent>(target, out var preStateComp))
                return false;

            RemComp<ZombieComponent>(target);
            RemComp<ZombifyOnDeathComponent>(target);
            RemComp<PendingZombieComponent>(target);

            _bloodstream.SetBloodLossThreshold(target, preStateComp.BloodlossThreshold);
            _faction.ClearFactions(target, dirty: false);
            foreach (var faction in preStateComp.OriginalFactions)
                _faction.AddFaction(target, faction);
            EnsureComp<ComplexInteractionComponent>(target);
            _nameMod.RefreshNameModifiers(target);
            _identity.QueueIdentityUpdate(target);
            _bloodstream.ChangeBloodReagents(target, preStateComp.BeforeZombifiedBloodReagents);

            RemComp<PreZombificationValuesComponent>(target);


            return true;
        }
    }
}

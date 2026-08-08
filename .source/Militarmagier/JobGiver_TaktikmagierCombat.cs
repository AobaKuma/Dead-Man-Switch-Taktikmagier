using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using AbilityDef = VEF.Abilities.AbilityDef;
using System.Linq;

namespace Militarmagier
{
    /// <summary>
    /// Lets AI-controlled magiers actually fight like magiers.
    ///
    /// VEF only feeds abilities to the AI through a postfix on <c>Pawn.TryGetAttackVerb</c>,
    /// which can only offer abilities whose verb can hit the current enemy pawn. That rules out
    /// every Self- and Location-targeted psycast in this path - Flash, Repel, EMP burst and
    /// quick draw were unreachable by any raider, which is most of what makes a Taktikmagier
    /// feel like one. This think node drives those four directly.
    ///
    /// Player pawns are deliberately excluded: they have gizmos and VEF's own autocast toggle,
    /// and having their psycasts fire itself would be an unpleasant surprise.
    /// </summary>
    public class JobGiver_TaktikmagierCombat : ThinkNode_JobGiver
    {
        /// <summary>How often a candidate pawn re-evaluates. Every tick is pointless - the
        /// cheapest abilities here still sit on a 600 tick cooldown.</summary>
        private const int CheckIntervalTicks = 30;

        /// <summary>Repel is an "get off me" button: fire it when this many hostiles are inside
        /// <see cref="SwarmedRadius"/>.</summary>
        private const int SwarmedCount = 2;
        private const float SwarmedRadius = 4.9f;

        /// <summary>EMP is near-useless against flesh, so only bother when mechs cluster up.</summary>
        private const int EmpMechCount = 2;

        /// <summary>Quick draw is an aim-speed buff - only worth it once the enemy is close.</summary>
        private const float QuickDrawRange = 9.9f;

        /// <summary>
        /// Quick draw sits at the bottom of the ladder, so it must not eat the neural heat the
        /// caster will want for Repel or Flash. VPE only ever stops a cast that would overflow
        /// entropy outright; reserving headroom for the abilities that actually matter is the
        /// part the AI has to decide for itself.
        /// </summary>
        private const float QuickDrawEntropyCeiling = 0.5f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            // --- cheap rejections first: this node sits in a constant think tree and is
            // evaluated for every humanlike on the map, so it must bail out fast. ---
            if (pawn?.Faction == null || pawn.Faction.IsPlayer)
            {
                return null;
            }
            if (!pawn.Spawned || pawn.Downed || pawn.Map == null)
            {
                return null;
            }
            if (!pawn.IsHashIntervalTick(CheckIntervalTicks))
            {
                return null;
            }

            Thing enemy = pawn.mindState?.enemyTarget;
            if (enemy == null || !enemy.Spawned || enemy.Map != pawn.Map)
            {
                return null;
            }

            CompAbilities comp = pawn.GetComp<CompAbilities>();
            if (comp == null || comp.currentlyCasting != null || comp.LearnedAbilities.NullOrEmpty())
            {
                return null;
            }
            // Never interrupt an ability cast that is already running.
            if (pawn.CurJobDef == MilitarmagierDefOf.VFEA_UseAbility)
            {
                return null;
            }

            // --- decision ladder, most urgent first ---
            //
            // Note on range checks below: Ability.CanHitTarget requires
            // distanceToTarget < GetRangeForPawn(). Repel, EMP burst and quick draw never
            // declare <range>, so their range is 0 and CanHitTarget(self) is 0 < 0 == FALSE.
            // Do NOT "tidy this up" by routing the self-cast branches through CanHitTarget or
            // ValidateTarget - that silently disables three of the four abilities. The real
            // targeting path never range-checks Self-mode abilities either.
            Job job;

            // 1. Being swarmed in melee - break contact before anything else.
            Ability repel = Ready(comp, MilitarmagierAbilityDefOf.DMST_Repel);
            if (repel != null && HostilesWithin(pawn, SwarmedRadius, mechsOnly: false) >= SwarmedCount)
            {
                job = TryCastJob(pawn, comp, repel, pawn);
                if (job != null)
                {
                    return job;
                }
            }

            // 2. A cluster of mechanoids in range of the burst.
            Ability emp = Ready(comp, MilitarmagierAbilityDefOf.DMST_EmpBurst);
            if (emp != null && HostilesWithin(pawn, emp.GetRadiusForPawn(), mechsOnly: true) >= EmpMechCount)
            {
                job = TryCastJob(pawn, comp, emp, pawn);
                if (job != null)
                {
                    return job;
                }
            }

            // 3. Blind the target before shooting into it. Location-targeted, so this one really
            //    does have a range and CanHitTarget is the right gate.
            Ability flash = Ready(comp, MilitarmagierAbilityDefOf.DMST_Flash);
            if (flash != null && flash.CanHitTarget(enemy.Position))
            {
                job = TryCastJob(pawn, comp, flash, enemy.Position);
                if (job != null)
                {
                    return job;
                }
            }

            // 4. Close range, no buff up yet, and enough neural headroom left that spending it
            //    here will not cost the caster its emergency options.
            Ability quickDraw = Ready(comp, MilitarmagierAbilityDefOf.DMST_QuickDraw);
            if (quickDraw != null
                && pawn.equipment?.Primary != null
                && !pawn.equipment.Primary.def.IsMeleeWeapon
                && !pawn.health.hediffSet.HasHediff(MilitarmagierDefOf.DMST_QuickDraw)
                && pawn.Position.InHorDistOf(enemy.Position, QuickDrawRange)
                && EntropyFraction(pawn) < QuickDrawEntropyCeiling)
            {
                job = TryCastJob(pawn, comp, quickDraw, pawn);
                if (job != null)
                {
                    return job;
                }
            }

            return null;
        }

        /// <summary>Current neural heat as a 0-1 fraction of the caster's ceiling.</summary>
        private static float EntropyFraction(Pawn pawn)
        {
            Pawn_PsychicEntropyTracker tracker = pawn.psychicEntropy;
            if (tracker == null)
            {
                return 1f;
            }

            float max = tracker.MaxEntropy;
            return max <= 0f ? 1f : tracker.EntropyValue / max;
        }

        /// <summary>The pawn's copy of <paramref name="def"/> if it is off cooldown and otherwise castable.</summary>
        private static Ability Ready(CompAbilities comp, AbilityDef def)
        {
            if (def == null)
            {
                return null;
            }

            List<Ability> abilities = comp.LearnedAbilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                Ability ability = abilities[i];
                if (ability?.def != def)
                {
                    continue;
                }
                // IsEnabledForPawn covers the cooldown and the psycast extension's own
                // psyfocus / entropy checks, so an exhausted magier will not try to cast.
                return ability.IsEnabledForPawn(out _) ? ability : null;
            }
            return null;
        }

        /// <summary>
        /// Builds the same job VEF's <c>Ability.StartAbilityJob</c> would, after running the same
        /// validation <c>Ability.CreateCastJob</c> runs. Returns null if the cast is refused.
        ///
        /// Two deliberate divergences from VEF's own path:
        ///
        /// * <c>EndCurrentJob</c> is not called. The think tree is already partway through
        ///   choosing the next job; ending the current one here would re-enter the job tracker.
        /// * <c>PreCast</c> is not called. Its contract hands the extension a callback to start
        ///   the job later, which a think node cannot honour - it must return a job now or
        ///   nothing. None of the four abilities driven here use a PreCast extension
        ///   (AbilityExtension_Psycast does not override it), so nothing is lost; if a future
        ///   ability needs one, it belongs on VEF's normal casting path instead of in here.
        /// </summary>
        private static Job TryCastJob(Pawn pawn, CompAbilities comp, Ability ability, LocalTargetInfo target)
        {
            GlobalTargetInfo[] targets = { target.ToGlobalTargetInfo(pawn.Map) };
            ability.ModifyTargets(ref targets);

            // The gate CreateCastJob applies before it will start a cast. AbilityExtension_Psycast
            // re-checks psyfocus, entropy overflow, psychic sensitivity and whether the caster is
            // already channelling, so anything that squeaked past the cheap Ready() filter is
            // caught here. throwMessages stays false: an AI decision must never put a rejection
            // message on the player's screen.
            List<AbilityExtension_AbilityMod> extensions = ability.AbilityModExtensions;
            for (int i = 0; i < extensions.Count; i++)
            {
                if (!extensions[i].Valid(targets, ability, false))
                {
                    return null;
                }
            }

            // VEF tracks the in-flight cast on the comp rather than on the Job, so it has to be
            // staged before the job is handed back.
            comp.currentlyCasting = ability;
            comp.currentlyCastingTargets = targets;

            Job job = JobMaker.MakeJob(ability.def.jobDef ?? MilitarmagierDefOf.VFEA_UseAbility, target);
            job.checkOverrideOnExpire = true;
            return job;
        }

        /// <summary>Counts live hostiles within <paramref name="radius"/> of the caster.</summary>
        private static int HostilesWithin(Pawn pawn, float radius, bool mechsOnly)
        {
            List<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned.ToList();
            float radiusSquared = radius * radius;
            int count = 0;

            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == pawn || other.Dead || other.Downed)
                {
                    continue;
                }
                if (mechsOnly && !other.RaceProps.IsMechanoid)
                {
                    continue;
                }
                if (!other.HostileTo(pawn))
                {
                    continue;
                }
                if ((other.Position - pawn.Position).LengthHorizontalSquared > radiusSquared)
                {
                    continue;
                }
                count++;
            }

            return count;
        }
    }
}

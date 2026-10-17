using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Militarmagier
{
    /// <summary>
    /// Lets AI-controlled magiers actually fight like magiers.
    ///
    /// Flash, repel, EMP burst and quick draw are not offered to vanilla's AI casting at all
    /// (aiCanUse is false on every magier spell), so this think node decides when raiders use them.
    ///
    /// Player pawns are deliberately excluded: they have gizmos, and having their psycasts fire by
    /// themselves would be an unpleasant surprise.
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
        /// caster will want for Repel or Flash. Vanilla only stops a cast that would overflow
        /// entropy outright; reserving headroom for the abilities that actually matter is the
        /// part the AI has to decide for itself.
        /// </summary>
        private const float QuickDrawEntropyCeiling = 0.5f;

        /// <summary>Vanilla's psyfocus comparison slack, from Psycast.GizmoDisabled.</summary>
        private const float PsyfocusCostTolerance = 0.0005f;

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
            if (pawn.abilities == null || pawn.abilities.abilities.NullOrEmpty() || pawn.psychicEntropy == null)
            {
                return null;
            }

            Thing enemy = pawn.mindState?.enemyTarget;
            if (enemy == null || !enemy.Spawned || enemy.Map != pawn.Map)
            {
                return null;
            }

            // Never interrupt an ability cast that is already running.
            if (pawn.CurJob?.ability != null)
            {
                return null;
            }

            // --- decision ladder, most urgent first ---
            //
            // Repel, EMP burst and quick draw are self-cast (targetRequired false). Vanilla's
            // Verb.CanHitTargetFrom returns targetParams.canTargetSelf when the target is the caster,
            // so they need no range check; flash is location-targeted and really does have a range.
            Job job;

            // 1. Being swarmed in melee - break contact before anything else.
            Ability repel = Ready(pawn, MilitarmagierAbilityDefOf.DMST_Repel);
            if (repel != null && HostilesWithin(pawn, SwarmedRadius, mechsOnly: false) >= SwarmedCount)
            {
                job = repel.GetJob(pawn, pawn);
                if (job != null)
                {
                    return job;
                }
            }

            // 2. A cluster of mechanoids in range of the burst.
            Ability emp = Ready(pawn, MilitarmagierAbilityDefOf.DMST_EmpBurst);
            if (emp != null && HostilesWithin(pawn, EmpRadius(emp), mechsOnly: true) >= EmpMechCount)
            {
                job = emp.GetJob(pawn, pawn);
                if (job != null)
                {
                    return job;
                }
            }

            // 3. Blind the target before shooting into it.
            Ability flash = Ready(pawn, MilitarmagierAbilityDefOf.DMST_Flash);
            if (flash != null && flash.verb.CanHitTarget(enemy.Position))
            {
                job = flash.GetJob(enemy.Position, enemy.Position);
                if (job != null)
                {
                    return job;
                }
            }

            // 4. Close range, no buff up yet, and enough neural headroom left that spending it
            //    here will not cost the caster its emergency options.
            Ability quickDraw = Ready(pawn, MilitarmagierAbilityDefOf.DMST_QuickDraw);
            if (quickDraw != null
                && pawn.equipment?.Primary != null
                && !pawn.equipment.Primary.def.IsMeleeWeapon
                && !pawn.health.hediffSet.HasHediff(MilitarmagierDefOf.DMST_QuickDraw)
                && pawn.Position.InHorDistOf(enemy.Position, QuickDrawRange)
                && EntropyFraction(pawn) < QuickDrawEntropyCeiling)
            {
                job = quickDraw.GetJob(pawn, pawn);
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
            float max = tracker.MaxEntropy;
            return max <= 0f ? 1f : tracker.EntropyValue / max;
        }

        private static float EmpRadius(Ability emp)
        {
            return emp.CompOfType<CompAbilityEffect_EmpBurst>()?.Props.radius ?? 0f;
        }

        /// <summary>
        /// The pawn's copy of <paramref name="def"/> if it can be cast right now, else null.
        ///
        /// Ability.GizmoDisabled cannot be used here: it refuses every undrafted pawn, and raiders are
        /// never drafted. CanCast covers comps (including the camouflage apparel check), cooldown, the
        /// psylink level and entropy overflow - but once a psycast has an entropy cost, Psycast.CanCast
        /// stops there, so the psyfocus cost and the psyfocus band limit are checked by hand.
        /// </summary>
        private static Ability Ready(Pawn pawn, AbilityDef def)
        {
            Ability ability = pawn.abilities.GetAbility(def);
            if (ability == null || !ability.CanCast)
            {
                return null;
            }

            Pawn_PsychicEntropyTracker tracker = pawn.psychicEntropy;
            if (def.PsyfocusCost > tracker.CurrentPsyfocus + PsyfocusCostTolerance)
            {
                return null;
            }
            if (def.level > tracker.MaxAbilityLevel)
            {
                return null;
            }
            return ability;
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

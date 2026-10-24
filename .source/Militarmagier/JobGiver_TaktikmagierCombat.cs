using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Militarmagier
{
    /// <summary>
    /// Lets AI-controlled magiers actually fight like magiers.
    ///
    /// No magier spell is offered to vanilla's AI casting (aiCanUse is false on all of them), so this
    /// think node decides when raiders use them.
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

        /// <summary>Smoke covers a retreat: the caster is hurt and was hit very recently.</summary>
        private const float SmokeHealthThreshold = 0.6f;
        private const int RecentlyHarmedTicks = 120;

        /// <summary>Ward and field repair go to allies below this summary health.</summary>
        private const float SupportHealthThreshold = 0.5f;

        /// <summary>EMP is near-useless against flesh, so only bother when mechs cluster up.</summary>
        private const int EmpMechCount = 2;

        /// <summary>Quick draw is for close range, steady aim for long range; nothing in between.</summary>
        private const float QuickDrawRange = 9.9f;
        private const float SteadyAimMinRange = 17.9f;

        /// <summary>
        /// The stances sit at the bottom of the ladder, so they must not eat the neural heat the
        /// caster will want for repel or flash. Vanilla only stops a cast that would overflow entropy
        /// outright; reserving headroom for the abilities that actually matter is the part the AI has
        /// to decide for itself.
        /// </summary>
        private const float StanceEntropyCeiling = 0.5f;

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
            // Repel, EMP burst and the stances are self-cast (targetRequired false). Vanilla's
            // Verb.CanHitTargetFrom returns targetParams.canTargetSelf when the target is the caster,
            // so they need no range check; everything aimed elsewhere goes through CanHitTarget.
            Job job;

            // 1. Being swarmed in melee - break contact before anything else.
            Ability repel = Ready(pawn, MilitarmagierAbilityDefOf.DMST_Repel);
            if (repel != null && HostilesWithin(pawn, SwarmedRadius, mechsOnly: false) >= SwarmedCount
                && (job = repel.GetJob(pawn, pawn)) != null)
            {
                return job;
            }

            // 2. Hurt and still taking hits - drop smoke on the spot to break line of sight.
            Ability smoke = Ready(pawn, MilitarmagierAbilityDefOf.DMST_Smoke);
            if (smoke != null
                && pawn.health.summaryHealth.SummaryHealthPercent < SmokeHealthThreshold
                && Find.TickManager.TicksGame - pawn.mindState.lastHarmTick < RecentlyHarmedTicks
                && smoke.verb.CanHitTarget(pawn.Position)
                && (job = smoke.GetJob(pawn.Position, pawn.Position)) != null)
            {
                return job;
            }

            // 3. A badly hurt ally in reach - put a ward on them.
            Ability ward = Ready(pawn, MilitarmagierAbilityDefOf.DMST_Ward);
            if (ward != null)
            {
                Pawn ally = FindAlly(pawn, ward, p =>
                    p.health.summaryHealth.SummaryHealthPercent < SupportHealthThreshold
                    && !p.health.hediffSet.HasHediff(MilitarmagierDefOf.DMST_Ward));
                if (ally != null && (job = ward.GetJob(ally, ally)) != null)
                {
                    return job;
                }
            }

            // 4. A cluster of mechanoids in range of the burst.
            Ability emp = Ready(pawn, MilitarmagierAbilityDefOf.DMST_EmpBurst);
            if (emp != null && HostilesWithin(pawn, EmpRadius(emp), mechsOnly: true) >= EmpMechCount
                && (job = emp.GetJob(pawn, pawn)) != null)
            {
                return job;
            }

            // 5. Blind the target before shooting into it.
            Ability flash = Ready(pawn, MilitarmagierAbilityDefOf.DMST_Flash);
            if (flash != null && flash.verb.CanHitTarget(enemy.Position)
                && (job = flash.GetJob(enemy.Position, enemy.Position)) != null)
            {
                return job;
            }

            // 6. An allied mech is falling apart - patch it up.
            Ability repair = Ready(pawn, MilitarmagierAbilityDefOf.DMST_FieldRepair);
            if (repair != null)
            {
                Pawn mech = FindAlly(pawn, repair, p =>
                    p.RaceProps.IsMechanoid
                    && p.health.summaryHealth.SummaryHealthPercent < SupportHealthThreshold);
                if (mech != null && (job = repair.GetJob(mech, mech)) != null)
                {
                    return job;
                }
            }

            // 7. Mark the target so the squad's fire hits harder.
            Ability mark = Ready(pawn, MilitarmagierAbilityDefOf.DMST_Mark);
            if (mark != null
                && enemy is Pawn enemyPawn
                && !enemyPawn.health.hediffSet.HasHediff(MilitarmagierDefOf.DMST_Marked)
                && mark.verb.CanHitTarget(enemy.Position)
                && (job = mark.GetJob(enemy.Position, enemy.Position)) != null)
            {
                return job;
            }

            // 8. Pick a shooting stance for the range the fight is at, with enough neural headroom
            //    left that spending it here will not cost the caster its emergency options.
            if (HasRangedWeapon(pawn) && EntropyFraction(pawn) < StanceEntropyCeiling)
            {
                bool close = pawn.Position.InHorDistOf(enemy.Position, QuickDrawRange);
                bool far = !pawn.Position.InHorDistOf(enemy.Position, SteadyAimMinRange);

                Ability quickDraw = close ? Ready(pawn, MilitarmagierAbilityDefOf.DMST_QuickDraw) : null;
                if (quickDraw != null
                    && !pawn.health.hediffSet.HasHediff(MilitarmagierDefOf.DMST_QuickDraw)
                    && (job = quickDraw.GetJob(pawn, pawn)) != null)
                {
                    return job;
                }

                Ability steadyAim = far ? Ready(pawn, MilitarmagierAbilityDefOf.DMST_SteadyAim) : null;
                if (steadyAim != null
                    && !pawn.health.hediffSet.HasHediff(MilitarmagierDefOf.DMST_SteadyAim)
                    && (job = steadyAim.GetJob(pawn, pawn)) != null)
                {
                    return job;
                }
            }

            return null;
        }

        private static bool HasRangedWeapon(Pawn pawn)
        {
            return pawn.equipment?.Primary != null && !pawn.equipment.Primary.def.IsMeleeWeapon;
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

        /// <summary>The first same-faction pawn (the caster included) the ability can reach that matches.</summary>
        private static Pawn FindAlly(Pawn pawn, Ability ability, Func<Pawn, bool> wanted)
        {
            IReadOnlyList<Pawn> allies = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
            for (int i = 0; i < allies.Count; i++)
            {
                Pawn ally = allies[i];
                if (ally.Dead || !wanted(ally))
                {
                    continue;
                }
                if (ability.verb.CanHitTarget(ally))
                {
                    return ally;
                }
            }
            return null;
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
